using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>The game as the frames package sees it, through the game's natives.</summary>
    internal sealed class NativeFramesWorld : IFramesWorld
    {
        /// <summary>Makes a BACKDROP painted blank under ConsoleUIBackdrop, over the 4:3 middle until the window's edges are known: a FRAME by type there takes every click on the world, even turned off, and a BACKDROP lets them through (seen 11-03).</summary>
        public framehandle ScreenRoot()
        {
            var root = Natives.BlzCreateFrameByType("BACKDROP", "MoonwellScreen", Natives.BlzGetFrameByName("ConsoleUIBackdrop", 0), "", 0);
            Natives.BlzFrameSetTexture(root, Image.Blank, 0, true);
            Natives.BlzFrameSetAbsPoint(root, Natives.FRAMEPOINT_BOTTOMLEFT, 0.0f, 0.0f);
            Natives.BlzFrameSetAbsPoint(root, Natives.FRAMEPOINT_TOPRIGHT, 0.8f, 0.6f);
            return root;
        }

        /// <summary>The trigger of each kind of frame event, made at its first use.</summary>
        private readonly Dictionary<int, trigger> triggers = new Dictionary<int, trigger>();

        /// <summary>Registers a frame for a kind of event on that kind's trigger: BlzTriggerRegisterFrameEvent.</summary>
        public void ListenFrame(framehandle frame, int kind, Action action)
        {
            if (!triggers.TryGetValue(kind, out var listening))
            {
                listening = Natives.CreateTrigger();
                Natives.TriggerAddAction(listening, action);
                triggers[kind] = listening;
            }

            Natives.BlzTriggerRegisterFrameEvent(listening, frame, Natives.ConvertFrameEventType(kind));
        }

        /// <summary>Makes a frame the tooltip of another: BlzFrameSetTooltip.</summary>
        public void TipFrame(framehandle frame, framehandle tooltip)
        {
            Natives.BlzFrameSetTooltip(frame, tooltip);
        }

        /// <summary>Adds a line to a frame's text: BlzFrameAddText.</summary>
        public void AddFrameText(framehandle frame, string line)
        {
            Natives.BlzFrameAddText(frame, line);
        }

        /// <summary>Sets a frame's value: BlzFrameSetValue.</summary>
        public void SetFrameValue(framehandle frame, float value)
        {
            Natives.BlzFrameSetValue(frame, value);
        }

        /// <summary>Sets a frame's range: BlzFrameSetMinMaxValue.</summary>
        public void SetFrameRange(framehandle frame, float min, float max)
        {
            Natives.BlzFrameSetMinMaxValue(frame, min, max);
        }

        /// <summary>Sets a frame's step: BlzFrameSetStepSize.</summary>
        public void SetFrameStep(framehandle frame, float step)
        {
            Natives.BlzFrameSetStepSize(frame, step);
        }

        /// <summary>Limits a frame's text: BlzFrameSetTextSizeLimit.</summary>
        public void LimitFrameText(framehandle frame, int characters)
        {
            Natives.BlzFrameSetTextSizeLimit(frame, characters);
        }

        /// <summary>Gives the keyboard back: BlzFrameSetEnable off, then on.</summary>
        public void ReleaseKeyboard(framehandle frame)
        {
            Natives.BlzFrameSetEnable(frame, false);
            Natives.BlzFrameSetEnable(frame, true);
        }

        /// <summary>The frame of the event: BlzGetTriggerFrame.</summary>
        public framehandle EventFrame() => Natives.BlzGetTriggerFrame();

        /// <summary>The player of the event: GetTriggerPlayer.</summary>
        public player EventPlayer() => Natives.GetTriggerPlayer();

        /// <summary>The value of the event: BlzGetTriggerFrameValue.</summary>
        public float EventValue() => Natives.BlzGetTriggerFrameValue();

        /// <summary>The text of the event: BlzGetTriggerFrameText.</summary>
        public string EventText() => Natives.BlzGetTriggerFrameText();

        /// <summary>Loads a TOC file: BlzLoadTOCFile.</summary>
        public bool LoadToc(string path)
        {
            return Natives.BlzLoadTOCFile(path);
        }

        /// <summary>Makes a frame from a template, BlzCreateFrame, or by type when the template is empty, BlzCreateFrameByType.</summary>
        public framehandle? MakeFrame(string type, string template, framehandle parent)
        {
            if (template.Length > 0)
            {
                return Natives.BlzCreateFrame(template, parent, 0, 0);
            }

            return Natives.BlzCreateFrameByType(type, "MoonwellFrame", parent, "", 0);
        }

        /// <summary>Shows or hides a frame on this machine: BlzFrameSetVisible.</summary>
        public void ShowFrame(framehandle frame, bool shown)
        {
            Natives.BlzFrameSetVisible(frame, shown);
        }

        /// <summary>Ties a point of a frame to a point of another: BlzFrameSetPoint.</summary>
        public void PlaceFrame(framehandle frame, int point, framehandle other, int otherPoint, float x, float y)
        {
            Natives.BlzFrameSetPoint(frame, Natives.ConvertFramePointType(point), other, Natives.ConvertFramePointType(otherPoint), x, y);
        }

        /// <summary>Clears a frame's points: BlzFrameClearAllPoints.</summary>
        public void ClearFramePoints(framehandle frame)
        {
            Natives.BlzFrameClearAllPoints(frame);
        }

        /// <summary>Sets a frame to cover another: BlzFrameSetAllPoints.</summary>
        public void FillFrame(framehandle frame, framehandle other)
        {
            Natives.BlzFrameSetAllPoints(frame, other);
        }

        /// <summary>Sizes a frame: BlzFrameSetSize.</summary>
        public void SizeFrame(framehandle frame, float width, float height)
        {
            Natives.BlzFrameSetSize(frame, width, height);
        }

        /// <summary>A frame's width: BlzFrameGetWidth.</summary>
        public float FrameWidth(framehandle frame)
        {
            return Natives.BlzFrameGetWidth(frame);
        }

        /// <summary>A frame's height: BlzFrameGetHeight.</summary>
        public float FrameHeight(framehandle frame)
        {
            return Natives.BlzFrameGetHeight(frame);
        }

        /// <summary>Scales a frame: BlzFrameSetScale.</summary>
        public void ScaleFrame(framehandle frame, float scale)
        {
            Natives.BlzFrameSetScale(frame, scale);
        }

        /// <summary>Lets a frame take the mouse, or not: BlzFrameSetEnable.</summary>
        public void EnableFrame(framehandle frame, bool enabled)
        {
            Natives.BlzFrameSetEnable(frame, enabled);
        }

        /// <summary>Sets a frame's opacity: BlzFrameSetAlpha.</summary>
        public void FadeFrame(framehandle frame, int alpha)
        {
            Natives.BlzFrameSetAlpha(frame, alpha);
        }

        /// <summary>Sets a frame's drawing level: BlzFrameSetLevel.</summary>
        public void LevelFrame(framehandle frame, int level)
        {
            Natives.BlzFrameSetLevel(frame, level);
        }

        /// <summary>Writes a frame's text: BlzFrameSetText.</summary>
        public void WriteFrameText(framehandle frame, string text)
        {
            Natives.BlzFrameSetText(frame, text);
        }

        /// <summary>Aligns a frame's text: BlzFrameSetTextAlignment.</summary>
        public void AlignFrameText(framehandle frame, int vertical, int horizontal)
        {
            Natives.BlzFrameSetTextAlignment(frame, Natives.ConvertTextAlignType(vertical), Natives.ConvertTextAlignType(horizontal));
        }

        /// <summary>Paints a frame with a texture: BlzFrameSetTexture, not tiled, with blending.</summary>
        public void PaintFrame(framehandle frame, string texture)
        {
            Natives.BlzFrameSetTexture(frame, texture, 0, true);
        }

        /// <summary>Sets where a frame's corners sit: BlzFrameSetAbsPoint at the bottom left and the top right.</summary>
        public void SpanFrame(framehandle frame, float left, float bottom, float right, float top)
        {
            Natives.BlzFrameSetAbsPoint(frame, Natives.FRAMEPOINT_BOTTOMLEFT, left, bottom);
            Natives.BlzFrameSetAbsPoint(frame, Natives.FRAMEPOINT_TOPRIGHT, right, top);
        }

        /// <summary>This machine's window's width: BlzGetLocalClientWidth.</summary>
        public int ClientWidth()
        {
            return Natives.BlzGetLocalClientWidth();
        }

        /// <summary>This machine's window's height: BlzGetLocalClientHeight.</summary>
        public int ClientHeight()
        {
            return Natives.BlzGetLocalClientHeight();
        }

        /// <summary>BlzFrameIsVisible.</summary>
        public bool FrameShown(framehandle frame) => Natives.BlzFrameIsVisible(frame);

        /// <summary>A frame of the game's HUD by its name: BlzGetFrameByName.</summary>
        public framehandle? GameFrame(string name)
        {
            return Natives.BlzGetFrameByName(name, 0);
        }

        /// <summary>A frame of the game's HUD by origin: BlzGetOriginFrame.</summary>
        public framehandle? OriginFrame(int kind, int index)
        {
            return Natives.BlzGetOriginFrame(Natives.ConvertOriginFrameType(kind), index);
        }

        /// <summary>Hides the game's HUD, or shows it: BlzHideOriginFrames.</summary>
        public void HideHud(bool hidden)
        {
            Natives.BlzHideOriginFrames(hidden);
        }

        /// <summary>Gives a frame another parent: BlzFrameSetParent.</summary>
        public void ParentFrame(framehandle frame, framehandle parent)
        {
            Natives.BlzFrameSetParent(frame, parent);
        }

        /// <summary>How many children a frame has: BlzFrameGetChildrenCount.</summary>
        public int ChildCount(framehandle frame)
        {
            return Natives.BlzFrameGetChildrenCount(frame);
        }

        /// <summary>A frame's child: BlzFrameGetChild.</summary>
        public framehandle ChildAt(framehandle frame, int index)
        {
            return Natives.BlzFrameGetChild(frame, index);
        }

        /// <summary>A frame's name: BlzFrameGetName.</summary>
        public string NameOf(framehandle frame)
        {
            return Natives.BlzFrameGetName(frame);
        }

        /// <summary>A frame's parent: BlzFrameGetParent.</summary>
        public framehandle ParentOf(framehandle frame)
        {
            return Natives.BlzFrameGetParent(frame);
        }

        /// <summary>Puts a point of a frame at a place on the screen: BlzFrameSetAbsPoint.</summary>
        public void PinFrame(framehandle frame, int point, float x, float y)
        {
            Natives.BlzFrameSetAbsPoint(frame, Natives.ConvertFramePointType(point), x, y);
        }

        /// <summary>Lets the game place its HUD's frames again, or stops it: BlzEnableUIAutoPosition.</summary>
        public void AutoPlaceHud(bool on)
        {
            Natives.BlzEnableUIAutoPosition(on);
        }

        /// <summary>Makes a BACKDROP painted blank under the game UI over the 4:3 middle, 0.8 by 0.6, which lets clicks through, as the screen root does.</summary>
        public framehandle OverRoot()
        {
            var root = Natives.BlzCreateFrameByType("BACKDROP", "MoonwellOver", Natives.BlzGetOriginFrame(Natives.ORIGIN_FRAME_GAME_UI, 0), "", 0);
            Natives.BlzFrameSetTexture(root, Image.Blank, 0, true);
            Natives.BlzFrameSetAbsPoint(root, Natives.FRAMEPOINT_BOTTOMLEFT, 0.0f, 0.0f);
            Natives.BlzFrameSetAbsPoint(root, Natives.FRAMEPOINT_TOPRIGHT, 0.8f, 0.6f);
            return root;
        }

        /// <summary>One trigger for every player slot's EVENT_PLAYER_MOUSE_UP.</summary>
        public void ListenMouseUp(Action action)
        {
            var trigger = Natives.CreateTrigger();
            for (var slot = 0; slot < Player.PlayableSlots; slot++)
            {
                Natives.TriggerRegisterPlayerEvent(trigger, Natives.Player(slot), Natives.EVENT_PLAYER_MOUSE_UP);
            }

            Natives.TriggerAddAction(trigger, action);
        }

        /// <summary>One trigger for every player slot's EVENT_PLAYER_END_CINEMATIC, Esc.</summary>
        public void ListenEscape(Action action)
        {
            var trigger = Natives.CreateTrigger();
            for (var slot = 0; slot < Player.PlayableSlots; slot++)
            {
                Natives.TriggerRegisterPlayerEvent(trigger, Natives.Player(slot), Natives.EVENT_PLAYER_END_CINEMATIC);
            }

            Natives.TriggerAddAction(trigger, action);
        }

        /// <summary>Whether the release was the left button's: BlzGetTriggerPlayerMouseButton against the game's constant.</summary>
        public bool EventLeftButton()
        {
            return Natives.BlzGetTriggerPlayerMouseButton() == Natives.MOUSE_BUTTON_TYPE_LEFT;
        }

        /// <summary>The cursor's x: BlzGetMouseScreenPosX.</summary>
        public int MouseScreenX()
        {
            return Natives.BlzGetMouseScreenPosX();
        }

        /// <summary>The cursor's y: BlzGetMouseScreenPosY.</summary>
        public int MouseScreenY()
        {
            return Natives.BlzGetMouseScreenPosY();
        }
    }
}
