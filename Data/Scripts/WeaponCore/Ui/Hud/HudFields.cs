﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage.Collections;
using VRage.Utils;
using VRageMath;
using VRageRender;
using WeaponCore.Platform;
using static VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    partial class Hud
    {
        private const float MetersInPixel = 0.0002645833f;
        private const float PaddingConst = 10 * MetersInPixel;
        private const float WeaponHudFontSize = 4f;
        private const float WeaponHudFontHeight = WeaponHudFontSize * MetersInPixel;
        private const float ReloadHeightConst = 4f * MetersInPixel;
        private const float ReloadWidthConst = ReloadHeightConst;
        private const float ReloadHeightOffsetConst = ReloadHeightConst;
        private const float HeatWidthConst = 35 * MetersInPixel;
        private const float HeatWidthOffset = HeatWidthConst + (PaddingConst * 1.8f);
        private const float HeatHeightConst = HeatWidthConst * 0.0625f;
        private const float InfoPanelOffset = WeaponHudFontHeight + (HeatHeightConst * 2f);
        private const float DefaultFov = 1.22f;
        private const float BgBorderRatio = .166f;
        private const float MonoWidthScaler = 0.75f;
        private const float ShadowWidthScaler = 0.7f;
        private const int InitialPoolCapacity = 512;
        private const uint MinUpdateTicks = 120;

        private readonly MyConcurrentPool<AgingTextRequest> _agingTextRequestPool = new MyConcurrentPool<AgingTextRequest>(64, data => data.Clean() );
        private readonly MyConcurrentPool<TextData> _textDataPool = new MyConcurrentPool<TextData>(128);
        private readonly ConcurrentQueue<TextureDrawData> _textureDrawPool = new ConcurrentQueue<TextureDrawData>();
        private readonly ConcurrentQueue<TextDrawRequest> _textDrawPool = new ConcurrentQueue<TextDrawRequest>();
        private readonly Queue<List<Weapon>> _weaponSortingListPool = new Queue<List<Weapon>>(InitialPoolCapacity);
        private readonly Queue<StackedWeaponInfo> _weaponStackedInfoPool = new Queue<StackedWeaponInfo>(InitialPoolCapacity);
        private readonly Queue<List<StackedWeaponInfo>> _weaponInfoListPool = new Queue<List<StackedWeaponInfo>>(InitialPoolCapacity);
        private readonly Queue<List<List<Weapon>>> _weaponSubListsPool = new Queue<List<List<Weapon>>>(InitialPoolCapacity);
        private readonly List<TextureDrawData> _textureAddList = new List<TextureDrawData>(256);
        private readonly List<TextDrawRequest> _textAddList = new List<TextDrawRequest>(256);
        private readonly List<TextureDrawData> _drawList = new List<TextureDrawData>(InitialPoolCapacity);
        private List<StackedWeaponInfo> _weapontoDraw = new List<StackedWeaponInfo>(256);

        private readonly ConcurrentDictionary<ElementNames, AgingTextRequest> _agingTextRequests = new ConcurrentDictionary<ElementNames, AgingTextRequest>();

        private readonly Session _session;
        private readonly MyStringId _monoEnglishFontAtlas1 = MyStringId.GetOrCompute("EnglishFontMono");
        private readonly MyStringId _shadowEnglishFontAtlas1 = MyStringId.GetOrCompute("EnglishFontShadow");

        private MatrixD _cameraWorldMatrix;
        private Vector2D _currWeaponDisplayPos;
        private Vector3D _viewPortSize;
        private uint _lastHudUpdateTick;


        private readonly TextureMap[] _reloadingTexture = new TextureMap[6];
        private readonly TextureMap[] _outofAmmoTexture = new TextureMap[2];
        private readonly TextureMap[] _chargingTexture = new TextureMap[10];
        private readonly TextureMap[] _infoBackground = new TextureMap[3];
        private readonly TextureMap[] _heatBarTexture = new TextureMap[11];
        private readonly Color _bgColor = new Color(40, 54, 62, 1);
        private readonly FontType _hudFont = FontType.Mono;
        private int _currentLargestName;
        private float _paddingHeat;
        private float _paddingReload;
        private float _padding;
        private float _reloadHeight;
        private float _reloadWidth;
        private float _reloadOffset;
        private float _heatOffsetX;
        private float _heatOffsetY;
        private float _textSize;
        private float _sTextSize;
        private float _textWidth;
        private float _stextWidth;
        private float _stackPadding;
        private float _heatWidth;
        private float _heatHeight;
        private float _infoPaneloffset;
        private float _bgWidth;
        private float _bgBorderHeight;
        private float _bgCenterHeight;
        private float _symbolWidth;
        private float _reloadHeightOffset;


        ///
        /// 
        ///

        internal readonly Dictionary<FontType, Dictionary<char, TextureMap>> CharacterMap;
        internal readonly List<Weapon> WeaponsToDisplay = new List<Weapon>(128);

        internal int TexturesToAdd;
        internal bool NeedsUpdate = true;
        internal bool AgingTextures;

        internal enum FontType
        {
            Mono,
            Shadow,
            Whitespace
        }

        internal enum ElementNames
        {
            Invalid,
            Element0,
            Element1,
            Element2,
            Element3,
            Element4,
            Element5,
            Element6,
            Element7,
            Element8,
        }

        internal enum Justify
        {
            None,
            Left,
            Center,
            Right,
        }

        internal Hud(Session session)
        {
            _session = session;


            LoadTextMaps("EN", out CharacterMap); // possible translations in future

            BuildMap(MyStringId.GetOrCompute("WeaponStatWindow"), 0, 0, 0, 128, 768, 128, 768, 384, ref _infoBackground);
            BuildMap(MyStringId.GetOrCompute("HeatAtlasBar"), 0, 0, 0, 64, 1024, 64, 1024, 1024, ref _heatBarTexture);
            BuildMap(MyStringId.GetOrCompute("ReloadingIcons"), 0, 0, 0, 64, 64, 64, 64, 512, ref _reloadingTexture);
            BuildMap(MyStringId.GetOrCompute("ReloadingIcons"), 0, 384, 0, 64, 64, 64, 64, 512, ref _outofAmmoTexture);
            BuildMap(MyStringId.GetOrCompute("RechargingIcons"), 0, 0, 0, 64, 64, 64, 64, 640, ref _chargingTexture);
            

            for (int i = 0; i < InitialPoolCapacity; i++)
            {
                _textureDrawPool.Enqueue(new TextureDrawData() { Position = new Vector3D(), Blend = PostPP });
                _textDrawPool.Enqueue(new TextDrawRequest() { Position = new Vector3D() });
                _weaponSortingListPool.Enqueue(new List<Weapon>());
                _weaponStackedInfoPool.Enqueue(new StackedWeaponInfo());
                _weaponInfoListPool.Enqueue(new List<StackedWeaponInfo>());
                _weaponSubListsPool.Enqueue(new List<List<Weapon>>());
            }
        }

        internal void BuildMap(MyStringId material,float initOffsetX, float initOffsetY, float offsetX, float OffsetY, float uvSizeX, float uvSizeY, float textureSizeX, float textureSizeY, ref TextureMap[] textureArr)
        {
            for (int i = 0; i < textureArr.Length; i++)
            {
                var offX = initOffsetX + (offsetX * i);
                var offY = initOffsetY + (OffsetY * i);
                textureArr[i] = GenerateMap(material, offX, offY, uvSizeX, uvSizeY, textureSizeX, textureSizeY);
            }
        }

        internal struct TextureMap
        {
            internal MyStringId Material;
            internal Vector2 P0;
            internal Vector2 P1;
            internal Vector2 P2;
            internal Vector2 P3;
        }


        internal class AgingTextRequest
        {
            internal readonly CachingList<TextData> Data = new CachingList<TextData>(32);
            internal string Text;
            internal Color Color;
            internal Vector3D Position;
            internal FontType Font;
            internal ElementNames Type;
            internal Justify Justify;
            internal float FontSize;
            internal float MessageWidth;
            internal float HeightScale;
            internal int Ttl;
            internal void Clean()
            {
                Data.Clear();
                MessageWidth = 0;
            }

            internal void RefreshTtl(int ttl)
            {
                Ttl = ttl;
            }
        }

        internal class TextData
        {
            internal MyStringId Material;
            internal Vector3D WorldPos;
            internal Vector2 P0;
            internal Vector2 P1;
            internal Vector2 P2;
            internal Vector2 P3;
            internal float ScaledWidth;
            internal bool UvDraw;
            internal bool TooWide;
            internal MyBillboard.BlendTypeEnum Blend = PostPP;
        }

        internal class StackedWeaponInfo
        {
            internal Weapon HighestValueWeapon;
            internal int WeaponStack;
            internal TextureDrawData CachedReloadTexture;
            internal TextureDrawData CachedHeatTexture;
            internal int ReloadIndex;
        }

        internal struct TextDrawRequest
        {
            internal string Text;
            internal Color Color;
            internal Vector3D Position;
            internal float FontSize;
            internal FontType Font;
        }

        internal class TextureDrawData
        {
            internal MyStringId Material;
            internal Color Color;
            internal Vector3D Position = new Vector3D();
            internal Vector3 Up;
            internal Vector3 Left;
            internal Vector2 P0;
            internal Vector2 P1;
            internal Vector2 P2;
            internal Vector2 P3;
            internal float Width;
            internal float Height;
            internal bool Persistant;
            internal bool UvDraw;
            internal MyBillboard.BlendTypeEnum Blend = PostPP;
        }
    }
}
