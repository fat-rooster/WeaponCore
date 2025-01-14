﻿using System.Diagnostics;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Session;

namespace WeaponCore
{
    internal class UiInput
    {
        internal int PreviousWheel;
        internal int CurrentWheel;
        internal int ShiftTime;
        internal bool MouseButtonPressed;
        internal bool InputChanged;
        internal bool MouseButtonLeftWasPressed;
        internal bool MouseButtonMenuWasPressed;
        internal bool MouseButtonRightWasPressed;
        internal bool WasInMenu;
        internal bool WheelForward;
        internal bool WheelBackward;
        internal bool ShiftReleased;
        internal bool ShiftPressed;
        internal bool LongShift;
        internal bool AltPressed;
        internal bool ActionKeyPressed;
        internal bool ActionKeyReleased;
        internal bool CtrlPressed;
        internal bool AnyKeyPressed;
        internal bool KeyPrevPressed;
        internal bool UiKeyPressed;
        internal bool UiKeyWasPressed;
        internal bool PlayerCamera;
        internal bool FirstPersonView;
        internal bool Debug = true;
        internal bool MouseShootWasOn;
        internal bool MouseShootOn;
        internal LineD AimRay;
        private readonly Session _session;
        private uint _lastInputUpdate;
        internal readonly InputStateData ClientInputState;
        internal MyKeys ActionKey;
        internal MyMouseButtonsEnum MouseButtonMenu;

        internal UiInput(Session session)
        {
            _session = session;
            ClientInputState = new InputStateData();
        }

        internal void UpdateInputState()
        {
            var s = _session;
            WheelForward = false;
            WheelBackward = false;
            AimRay = new LineD();

            if (!s.InGridAiBlock) s.UpdateLocalAiAndCockpit();

            if (s.InGridAiBlock && !s.InMenu)
            {
                MouseButtonPressed = MyAPIGateway.Input.IsAnyMousePressed();

                MouseButtonLeftWasPressed = ClientInputState.MouseButtonLeft;
                MouseButtonMenuWasPressed = ClientInputState.MouseButtonMenu;
                MouseButtonRightWasPressed = ClientInputState.MouseButtonRight;

                WasInMenu = ClientInputState.InMenu;
                ClientInputState.InMenu = _session.InMenu;

                if (MouseButtonPressed)
                {
                    ClientInputState.MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                    ClientInputState.MouseButtonMenu = MyAPIGateway.Input.IsMousePressed(MouseButtonMenu);
                    ClientInputState.MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);
                }
                else
                {
                    ClientInputState.MouseButtonLeft = false;
                    ClientInputState.MouseButtonMenu = false;
                    ClientInputState.MouseButtonRight = false;
                }

                _session.PlayerMouseStates[_session.PlayerId] = ClientInputState;

                if (_session.MpActive)  {
                    var shootButtonActive = ClientInputState.MouseButtonLeft || ClientInputState.MouseButtonRight;

                    MouseShootWasOn = MouseShootOn;
                    if ((_session.ManualShot  || s.Tick - _lastInputUpdate >= 29) && shootButtonActive && !MouseShootOn)
                    {
                        _lastInputUpdate = s.Tick; 
                        MouseShootOn = true;
                    }
                    else if (MouseShootOn && !shootButtonActive)
                        MouseShootOn = false;

                    InputChanged = MouseShootOn != MouseShootWasOn || WasInMenu != ClientInputState.InMenu;
                    _session.ManualShot = false;
                }

                ShiftReleased = MyAPIGateway.Input.IsNewKeyReleased(MyKeys.LeftShift);
                ShiftPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.LeftShift);
                ActionKeyReleased = MyAPIGateway.Input.IsNewKeyReleased(ActionKey);

                if (ShiftPressed)
                {
                    ShiftTime++;
                    LongShift = ShiftTime > 59;
                }
                else
                {
                    if (LongShift) ShiftReleased = false;
                    ShiftTime = 0;
                    LongShift = false;
                }

                AltPressed = MyAPIGateway.Input.IsAnyAltKeyPressed();
                CtrlPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);
                KeyPrevPressed = AnyKeyPressed;
                AnyKeyPressed = MyAPIGateway.Input.IsAnyKeyPress();
                UiKeyWasPressed = UiKeyPressed;
                UiKeyPressed = CtrlPressed || AltPressed || ShiftPressed;
                PlayerCamera = MyAPIGateway.Session.IsCameraControlledObject;
                FirstPersonView = PlayerCamera && MyAPIGateway.Session.CameraController.IsInFirstPersonView;

                if ((!UiKeyPressed && !UiKeyWasPressed) || !AltPressed && CtrlPressed && !FirstPersonView)
                {
                    PreviousWheel = MyAPIGateway.Input.PreviousMouseScrollWheelValue();
                    CurrentWheel = MyAPIGateway.Input.MouseScrollWheelValue();
                }
            }
            else if (!s.InMenu)
            {
                CtrlPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);
                ActionKeyPressed = MyAPIGateway.Input.IsKeyPress(ActionKey);

                if (CtrlPressed && ActionKeyPressed && GetAimRay(s, out AimRay) && Debug)
                {
                    DsDebugDraw.DrawLine(AimRay, Color.Red, 0.1f);
                }
            }

            if (_session.MpActive && !s.InGridAiBlock)
            {
                if (ClientInputState.InMenu || ClientInputState.MouseButtonRight ||  ClientInputState.MouseButtonMenu || ClientInputState.MouseButtonRight)
                {
                    ClientInputState.InMenu = false;
                    ClientInputState.MouseButtonLeft = false;
                    ClientInputState.MouseButtonMenu = false;
                    ClientInputState.MouseButtonRight = false;
                    InputChanged = true;
                }
            }

            if (CurrentWheel != PreviousWheel && CurrentWheel > PreviousWheel)
                WheelForward = true;
            else if (s.UiInput.CurrentWheel != s.UiInput.PreviousWheel)
                WheelBackward = true;
        }

        internal bool GetAimRay(Session s, out LineD ray)
        {
            var character = MyAPIGateway.Session.Player.Character;
            if (character != null)
            {
                ray = new LineD(s.PlayerPos, s.PlayerPos + (character.WorldMatrix.Forward * 1000000));
                return true;
            }
            ray = new LineD();
            return false;
        }
    }
}
