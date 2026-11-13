using System;

namespace Moonwell.Hosting
{
    /// <summary>The game as the frames package sees it: the frames it makes and what it sets on them. Types are the game's.</summary>
    internal interface IFramesWorld
    {
        /// <summary>Makes the whole-width root: a frame under the console's backdrop, which reaches the window's edges and draws beneath the HUD.</summary>
        framehandle ScreenRoot();

        /// <summary>Makes the over-the-HUD root: a frame under the game UI covering the 4:3 middle, drawn over the HUD.</summary>
        framehandle OverRoot();

        /// <summary>Registers a frame for a kind of event, the game's FRAMEEVENT_ number: one trigger a kind, made at its first use, which runs the action given then; inside it EventFrame, EventPlayer, EventValue and EventText are the event's.</summary>
        void ListenFrame(framehandle frame, int kind, Action action);

        /// <summary>Makes a frame the tooltip of another, which the game shows while the mouse is on it, on that machine alone.</summary>
        void TipFrame(framehandle frame, framehandle tooltip);

        /// <summary>Adds a line to a frame's text: a text area's.</summary>
        void AddFrameText(framehandle frame, string line);

        /// <summary>Sets a frame's value: a slider's.</summary>
        void SetFrameValue(framehandle frame, float value);

        /// <summary>Sets a frame's range: a slider's.</summary>
        void SetFrameRange(framehandle frame, float min, float max);

        /// <summary>Sets a frame's step: a slider's.</summary>
        void SetFrameStep(framehandle frame, float step);

        /// <summary>Limits a frame's text to a number of characters: a text box's.</summary>
        void LimitFrameText(framehandle frame, int characters);

        /// <summary>Gives the keyboard back to the game from a frame clicked on this machine: off, then on (seen 11-03).</summary>
        void ReleaseKeyboard(framehandle frame);

        /// <summary>The frame of the event being delivered.</summary>
        framehandle EventFrame();

        /// <summary>The player of the event being delivered.</summary>
        player EventPlayer();

        /// <summary>The value of the event being delivered: a slider's, a check box's, the wheel's.</summary>
        float EventValue();

        /// <summary>The text of the event being delivered: a text box's.</summary>
        string EventText();

        /// <summary>Loads a TOC file, which makes the templates of the files it lists; true when the game loaded it.</summary>
        bool LoadToc(string path);

        /// <summary>Makes a frame from a template, or by type when the template is empty, under a parent; null when the game has no such template (game facts).</summary>
        framehandle? MakeFrame(string type, string template, framehandle parent);

        /// <summary>Shows or hides a frame on this machine.</summary>
        void ShowFrame(framehandle frame, bool shown);

        /// <summary>Ties a point of a frame to a point of another, moved by an offset; points are numbered 0 top left to 8 bottom right.</summary>
        void PlaceFrame(framehandle frame, int point, framehandle other, int otherPoint, float x, float y);

        /// <summary>Clears a frame's points.</summary>
        void ClearFramePoints(framehandle frame);

        /// <summary>Sets a frame to cover another, all points.</summary>
        void FillFrame(framehandle frame, framehandle other);

        /// <summary>Sizes a frame.</summary>
        void SizeFrame(framehandle frame, float width, float height);

        /// <summary>A frame's width, as this machine reads it: a template's at once, one from points only later (seen 11-05).</summary>
        float FrameWidth(framehandle frame);

        /// <summary>A frame's height, as FrameWidth reads.</summary>
        float FrameHeight(framehandle frame);

        /// <summary>Scales a frame, with its children.</summary>
        void ScaleFrame(framehandle frame, float scale);

        /// <summary>Lets a frame take the mouse, or not.</summary>
        void EnableFrame(framehandle frame, bool enabled);

        /// <summary>Sets a frame's opacity, 0 to 255.</summary>
        void FadeFrame(framehandle frame, int alpha);

        /// <summary>Sets a frame's drawing level among its siblings.</summary>
        void LevelFrame(framehandle frame, int level);

        /// <summary>Writes a frame's text.</summary>
        void WriteFrameText(framehandle frame, string text);

        /// <summary>Aligns a frame's text, as the game numbers it: vertical 0 top, 1 middle, 2 bottom; horizontal 3 left, 4 centre, 5 right.</summary>
        void AlignFrameText(framehandle frame, int vertical, int horizontal);

        /// <summary>Paints a frame with a texture.</summary>
        void PaintFrame(framehandle frame, string texture);

        /// <summary>Sets where a frame's corners sit on the screen: its bottom left at left, bottom and its top right at right, top.</summary>
        void SpanFrame(framehandle frame, float left, float bottom, float right, float top);

        /// <summary>This machine's window's width, in pixels; differs between machines.</summary>
        int ClientWidth();

        /// <summary>This machine's window's height, in pixels; differs between machines.</summary>
        int ClientHeight();

        /// <summary>Whether the game shows a frame on this machine; a tooltip's while the mouse is on its frame, so it differs between machines.</summary>
        bool FrameShown(framehandle frame);

        /// <summary>A frame of the game's HUD by its name; null when the game has none. The first lookup gives it a handle id, so it is done on every machine.</summary>
        framehandle? GameFrame(string name);

        /// <summary>A frame of the game's HUD by the game's ORIGIN_FRAME_ number and an index; null when the game has none, as for the minimap (seen 11-05).</summary>
        framehandle? OriginFrame(int kind, int index);

        /// <summary>Hides the game's HUD on this machine, or shows it again; the command buttons and life bars stay.</summary>
        void HideHud(bool hidden);

        /// <summary>Gives a frame another parent.</summary>
        void ParentFrame(framehandle frame, framehandle parent);

        /// <summary>How many children a frame has: those its template gave it, and those made under it.</summary>
        int ChildCount(framehandle frame);

        /// <summary>A frame's child by its index, from 0; the first time gives it a handle id, so it is asked on every machine.</summary>
        framehandle ChildAt(framehandle frame, int index);

        /// <summary>A frame's name, as its .fdf gives it; the same on every machine.</summary>
        string NameOf(framehandle frame);

        /// <summary>A frame's parent; the first time gives it a handle id, so it is asked on every machine.</summary>
        framehandle ParentOf(framehandle frame);

        /// <summary>Puts a point of a frame at a place on the screen, in the 4:3 middle's units.</summary>
        void PinFrame(framehandle frame, int point, float x, float y);

        /// <summary>Lets the game place its HUD's frames again when the window changes, or stops it, on this machine.</summary>
        void AutoPlaceHud(bool on);

        /// <summary>Listens for every player slot's mouse releases, on one trigger; inside it EventPlayer is the player and EventLeftButton whether it was the left button.</summary>
        void ListenMouseUp(Action action);

        /// <summary>Listens for every player slot's Esc, on one trigger; inside it EventPlayer is the player.</summary>
        void ListenEscape(Action action);

        /// <summary>Whether the mouse release being delivered was the left button's.</summary>
        bool EventLeftButton();

        /// <summary>The cursor's x on this machine's window, in pixels from its left; differs between machines.</summary>
        int MouseScreenX();

        /// <summary>The cursor's y on this machine's window, in pixels from its top (seen 11-07); differs between machines.</summary>
        int MouseScreenY();
    }
}
