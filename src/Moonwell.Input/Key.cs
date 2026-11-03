namespace Moonwell
{
    /// <summary>
    /// A key of the keyboard, numbered by the code the game gives it (its OSKEY constants). The names are .NET's, and the
    /// game's where .NET has none. The punctuation keys keep their numbers (Oem1 to Oem8, Oem102), as each keyboard layout
    /// puts other characters on them.
    /// </summary>
    public enum Key
    {
        /// <summary>Backspace (OSKEY_BACKSPACE).</summary>
        Back = 8,

        /// <summary>Tab (OSKEY_TAB).</summary>
        Tab = 9,

        /// <summary>Clear (5 on the number pad with Num Lock off) (OSKEY_CLEAR).</summary>
        Clear = 12,

        /// <summary>Enter (OSKEY_RETURN).</summary>
        Enter = 13,

        /// <summary>Pause (OSKEY_PAUSE).</summary>
        Pause = 19,

        /// <summary>Caps Lock (OSKEY_CAPSLOCK).</summary>
        CapsLock = 20,

        /// <summary>Kana or Hangul mode, on Japanese and Korean keyboards (OSKEY_KANA, OSKEY_HANGUL).</summary>
        KanaMode = 21,

        /// <summary>Junja mode, on Korean keyboards (OSKEY_JUNJA).</summary>
        JunjaMode = 23,

        /// <summary>Final mode, on Korean keyboards (OSKEY_FINAL).</summary>
        FinalMode = 24,

        /// <summary>Hanja or Kanji mode, on Korean and Japanese keyboards (OSKEY_HANJA, OSKEY_KANJI).</summary>
        HanjaMode = 25,

        /// <summary>Esc (OSKEY_ESCAPE).</summary>
        Escape = 27,

        /// <summary>IME Convert (OSKEY_CONVERT).</summary>
        ImeConvert = 28,

        /// <summary>IME Nonconvert (OSKEY_NONCONVERT).</summary>
        ImeNonConvert = 29,

        /// <summary>IME Accept (OSKEY_ACCEPT).</summary>
        ImeAccept = 30,

        /// <summary>IME mode change (OSKEY_MODECHANGE).</summary>
        ImeModeChange = 31,

        /// <summary>Space bar (OSKEY_SPACE).</summary>
        Space = 32,

        /// <summary>Page Up (OSKEY_PAGEUP).</summary>
        PageUp = 33,

        /// <summary>Page Down (OSKEY_PAGEDOWN).</summary>
        PageDown = 34,

        /// <summary>End (OSKEY_END).</summary>
        End = 35,

        /// <summary>Home (OSKEY_HOME).</summary>
        Home = 36,

        /// <summary>Left arrow (OSKEY_LEFT).</summary>
        Left = 37,

        /// <summary>Up arrow (OSKEY_UP).</summary>
        Up = 38,

        /// <summary>Right arrow (OSKEY_RIGHT).</summary>
        Right = 39,

        /// <summary>Down arrow (OSKEY_DOWN).</summary>
        Down = 40,

        /// <summary>Select (OSKEY_SELECT).</summary>
        Select = 41,

        /// <summary>Print (OSKEY_PRINT).</summary>
        Print = 42,

        /// <summary>Execute (OSKEY_EXECUTE).</summary>
        Execute = 43,

        /// <summary>Print Screen (OSKEY_PRINTSCREEN).</summary>
        PrintScreen = 44,

        /// <summary>Insert (OSKEY_INSERT).</summary>
        Insert = 45,

        /// <summary>Delete (OSKEY_DELETE).</summary>
        Delete = 46,

        /// <summary>Help (OSKEY_HELP).</summary>
        Help = 47,

        /// <summary>0, on the row above the letters (OSKEY_0).</summary>
        D0 = 48,

        /// <summary>1, on the row above the letters (OSKEY_1).</summary>
        D1 = 49,

        /// <summary>2, on the row above the letters (OSKEY_2).</summary>
        D2 = 50,

        /// <summary>3, on the row above the letters (OSKEY_3).</summary>
        D3 = 51,

        /// <summary>4, on the row above the letters (OSKEY_4).</summary>
        D4 = 52,

        /// <summary>5, on the row above the letters (OSKEY_5).</summary>
        D5 = 53,

        /// <summary>6, on the row above the letters (OSKEY_6).</summary>
        D6 = 54,

        /// <summary>7, on the row above the letters (OSKEY_7).</summary>
        D7 = 55,

        /// <summary>8, on the row above the letters (OSKEY_8).</summary>
        D8 = 56,

        /// <summary>9, on the row above the letters (OSKEY_9).</summary>
        D9 = 57,

        /// <summary>A (OSKEY_A).</summary>
        A = 65,

        /// <summary>B (OSKEY_B).</summary>
        B = 66,

        /// <summary>C (OSKEY_C).</summary>
        C = 67,

        /// <summary>D (OSKEY_D).</summary>
        D = 68,

        /// <summary>E (OSKEY_E).</summary>
        E = 69,

        /// <summary>F (OSKEY_F).</summary>
        F = 70,

        /// <summary>G (OSKEY_G).</summary>
        G = 71,

        /// <summary>H (OSKEY_H).</summary>
        H = 72,

        /// <summary>I (OSKEY_I).</summary>
        I = 73,

        /// <summary>J (OSKEY_J).</summary>
        J = 74,

        /// <summary>K (OSKEY_K).</summary>
        K = 75,

        /// <summary>L (OSKEY_L).</summary>
        L = 76,

        /// <summary>M (OSKEY_M).</summary>
        M = 77,

        /// <summary>N (OSKEY_N).</summary>
        N = 78,

        /// <summary>O (OSKEY_O).</summary>
        O = 79,

        /// <summary>P (OSKEY_P).</summary>
        P = 80,

        /// <summary>Q (OSKEY_Q).</summary>
        Q = 81,

        /// <summary>R (OSKEY_R).</summary>
        R = 82,

        /// <summary>S (OSKEY_S).</summary>
        S = 83,

        /// <summary>T (OSKEY_T).</summary>
        T = 84,

        /// <summary>U (OSKEY_U).</summary>
        U = 85,

        /// <summary>V (OSKEY_V).</summary>
        V = 86,

        /// <summary>W (OSKEY_W).</summary>
        W = 87,

        /// <summary>X (OSKEY_X).</summary>
        X = 88,

        /// <summary>Y (OSKEY_Y).</summary>
        Y = 89,

        /// <summary>Z (OSKEY_Z).</summary>
        Z = 90,

        /// <summary>Left Windows (OSKEY_LMETA).</summary>
        LWin = 91,

        /// <summary>Right Windows (OSKEY_RMETA).</summary>
        RWin = 92,

        /// <summary>Menu (the application key) (OSKEY_APPS).</summary>
        Apps = 93,

        /// <summary>Sleep (OSKEY_SLEEP).</summary>
        Sleep = 95,

        /// <summary>0 on the number pad (OSKEY_NUMPAD0).</summary>
        NumPad0 = 96,

        /// <summary>1 on the number pad (OSKEY_NUMPAD1).</summary>
        NumPad1 = 97,

        /// <summary>2 on the number pad (OSKEY_NUMPAD2).</summary>
        NumPad2 = 98,

        /// <summary>3 on the number pad (OSKEY_NUMPAD3).</summary>
        NumPad3 = 99,

        /// <summary>4 on the number pad (OSKEY_NUMPAD4).</summary>
        NumPad4 = 100,

        /// <summary>5 on the number pad (OSKEY_NUMPAD5).</summary>
        NumPad5 = 101,

        /// <summary>6 on the number pad (OSKEY_NUMPAD6).</summary>
        NumPad6 = 102,

        /// <summary>7 on the number pad (OSKEY_NUMPAD7).</summary>
        NumPad7 = 103,

        /// <summary>8 on the number pad (OSKEY_NUMPAD8).</summary>
        NumPad8 = 104,

        /// <summary>9 on the number pad (OSKEY_NUMPAD9).</summary>
        NumPad9 = 105,

        /// <summary>* on the number pad (OSKEY_MULTIPLY).</summary>
        Multiply = 106,

        /// <summary>+ on the number pad (OSKEY_ADD).</summary>
        Add = 107,

        /// <summary>Separator on the number pad (OSKEY_SEPARATOR).</summary>
        Separator = 108,

        /// <summary>- on the number pad (OSKEY_SUBTRACT).</summary>
        Subtract = 109,

        /// <summary>Decimal point on the number pad (OSKEY_DECIMAL).</summary>
        Decimal = 110,

        /// <summary>/ on the number pad (OSKEY_DIVIDE).</summary>
        Divide = 111,

        /// <summary>F1 (OSKEY_F1).</summary>
        F1 = 112,

        /// <summary>F2 (OSKEY_F2).</summary>
        F2 = 113,

        /// <summary>F3 (OSKEY_F3).</summary>
        F3 = 114,

        /// <summary>F4 (OSKEY_F4).</summary>
        F4 = 115,

        /// <summary>F5 (OSKEY_F5).</summary>
        F5 = 116,

        /// <summary>F6 (OSKEY_F6).</summary>
        F6 = 117,

        /// <summary>F7 (OSKEY_F7).</summary>
        F7 = 118,

        /// <summary>F8 (OSKEY_F8).</summary>
        F8 = 119,

        /// <summary>F9 (OSKEY_F9).</summary>
        F9 = 120,

        /// <summary>F10 (OSKEY_F10).</summary>
        F10 = 121,

        /// <summary>F11 (OSKEY_F11).</summary>
        F11 = 122,

        /// <summary>F12 (OSKEY_F12).</summary>
        F12 = 123,

        /// <summary>F13 (OSKEY_F13).</summary>
        F13 = 124,

        /// <summary>F14 (OSKEY_F14).</summary>
        F14 = 125,

        /// <summary>F15 (OSKEY_F15).</summary>
        F15 = 126,

        /// <summary>F16 (OSKEY_F16).</summary>
        F16 = 127,

        /// <summary>F17 (OSKEY_F17).</summary>
        F17 = 128,

        /// <summary>F18 (OSKEY_F18).</summary>
        F18 = 129,

        /// <summary>F19 (OSKEY_F19).</summary>
        F19 = 130,

        /// <summary>F20 (OSKEY_F20).</summary>
        F20 = 131,

        /// <summary>F21 (OSKEY_F21).</summary>
        F21 = 132,

        /// <summary>F22 (OSKEY_F22).</summary>
        F22 = 133,

        /// <summary>F23 (OSKEY_F23).</summary>
        F23 = 134,

        /// <summary>F24 (OSKEY_F24).</summary>
        F24 = 135,

        /// <summary>Num Lock (OSKEY_NUMLOCK).</summary>
        NumLock = 144,

        /// <summary>Scroll Lock (OSKEY_SCROLLLOCK).</summary>
        Scroll = 145,

        /// <summary>= on the NEC number pad, or Jisho on Fujitsu keyboards (OSKEY_OEM_NEC_EQUAL, OSKEY_OEM_FJ_JISHO).</summary>
        OemNecEqual = 146,

        /// <summary>Masshou on Fujitsu keyboards (OSKEY_OEM_FJ_MASSHOU).</summary>
        OemFjMasshou = 147,

        /// <summary>Touroku on Fujitsu keyboards (OSKEY_OEM_FJ_TOUROKU).</summary>
        OemFjTouroku = 148,

        /// <summary>Left OYAYUBI on Fujitsu keyboards (OSKEY_OEM_FJ_LOYA).</summary>
        OemFjLoya = 149,

        /// <summary>Right OYAYUBI on Fujitsu keyboards (OSKEY_OEM_FJ_ROYA).</summary>
        OemFjRoya = 150,

        /// <summary>Left Shift (OSKEY_LSHIFT).</summary>
        LeftShift = 160,

        /// <summary>Right Shift (OSKEY_RSHIFT).</summary>
        RightShift = 161,

        /// <summary>Left Ctrl (OSKEY_LCONTROL).</summary>
        LeftCtrl = 162,

        /// <summary>Right Ctrl (OSKEY_RCONTROL).</summary>
        RightCtrl = 163,

        /// <summary>Left Alt (OSKEY_LALT).</summary>
        LeftAlt = 164,

        /// <summary>Right Alt (AltGr) (OSKEY_RALT).</summary>
        RightAlt = 165,

        /// <summary>Browser Back (OSKEY_BROWSER_BACK).</summary>
        BrowserBack = 166,

        /// <summary>Browser Forward (OSKEY_BROWSER_FORWARD).</summary>
        BrowserForward = 167,

        /// <summary>Browser Refresh (OSKEY_BROWSER_REFRESH).</summary>
        BrowserRefresh = 168,

        /// <summary>Browser Stop (OSKEY_BROWSER_STOP).</summary>
        BrowserStop = 169,

        /// <summary>Browser Search (OSKEY_BROWSER_SEARCH).</summary>
        BrowserSearch = 170,

        /// <summary>Browser Favorites (OSKEY_BROWSER_FAVORITES).</summary>
        BrowserFavorites = 171,

        /// <summary>Browser Home (OSKEY_BROWSER_HOME).</summary>
        BrowserHome = 172,

        /// <summary>Volume Mute (OSKEY_VOLUME_MUTE).</summary>
        VolumeMute = 173,

        /// <summary>Volume Down (OSKEY_VOLUME_DOWN).</summary>
        VolumeDown = 174,

        /// <summary>Volume Up (OSKEY_VOLUME_UP).</summary>
        VolumeUp = 175,

        /// <summary>Next Track (OSKEY_MEDIA_NEXT_TRACK).</summary>
        MediaNextTrack = 176,

        /// <summary>Previous Track (OSKEY_MEDIA_PREV_TRACK).</summary>
        MediaPreviousTrack = 177,

        /// <summary>Stop Media (OSKEY_MEDIA_STOP).</summary>
        MediaStop = 178,

        /// <summary>Play/Pause Media (OSKEY_MEDIA_PLAY_PAUSE).</summary>
        MediaPlayPause = 179,

        /// <summary>Start Mail (OSKEY_LAUNCH_MAIL).</summary>
        LaunchMail = 180,

        /// <summary>Select Media (OSKEY_LAUNCH_MEDIA_SELECT).</summary>
        SelectMedia = 181,

        /// <summary>Start Application 1 (OSKEY_LAUNCH_APP1).</summary>
        LaunchApplication1 = 182,

        /// <summary>Start Application 2 (OSKEY_LAUNCH_APP2).</summary>
        LaunchApplication2 = 183,

        /// <summary>OEM 1; on a US keyboard, ;; another character on other layouts (OSKEY_OEM_1).</summary>
        Oem1 = 186,

        /// <summary>+ or =; on a US keyboard, =; another character on other layouts (OSKEY_OEM_PLUS).</summary>
        OemPlus = 187,

        /// <summary>, (comma) on a US keyboard; another character on other layouts (OSKEY_OEM_COMMA).</summary>
        OemComma = 188,

        /// <summary>- (minus) on a US keyboard; another character on other layouts, + on a Swedish one (OSKEY_OEM_MINUS).</summary>
        OemMinus = 189,

        /// <summary>. (full stop) on a US keyboard; another character on other layouts (OSKEY_OEM_PERIOD).</summary>
        OemPeriod = 190,

        /// <summary>OEM 2; on a US keyboard, /; another character on other layouts (OSKEY_OEM_2).</summary>
        Oem2 = 191,

        /// <summary>OEM 3; on a US keyboard, the backtick; another character on other layouts (OSKEY_OEM_3).</summary>
        Oem3 = 192,

        /// <summary>OEM 4; on a US keyboard, [; another character on other layouts (OSKEY_OEM_4).</summary>
        Oem4 = 219,

        /// <summary>OEM 5; on a US keyboard, backslash; another character on other layouts (OSKEY_OEM_5).</summary>
        Oem5 = 220,

        /// <summary>OEM 6; on a US keyboard, ]; another character on other layouts (OSKEY_OEM_6).</summary>
        Oem6 = 221,

        /// <summary>OEM 7; on a US keyboard, the quote; another character on other layouts (OSKEY_OEM_7).</summary>
        Oem7 = 222,

        /// <summary>OEM 8, which a US keyboard does not have (OSKEY_OEM_8).</summary>
        Oem8 = 223,

        /// <summary>AX on Japanese AX keyboards (OSKEY_OEM_AX).</summary>
        OemAx = 225,

        /// <summary>OEM 102; on a European keyboard, the key beside the left Shift (&lt; or backslash) (OSKEY_OEM_102).</summary>
        Oem102 = 226,

        /// <summary>Help on Olivetti keyboards (OSKEY_ICO_HELP).</summary>
        IcoHelp = 227,

        /// <summary>00 on Olivetti keyboards (OSKEY_ICO_00).</summary>
        Ico00 = 228,

        /// <summary>IME Process (OSKEY_PROCESSKEY).</summary>
        ImeProcessed = 229,

        /// <summary>Clear on Olivetti keyboards (OSKEY_ICO_CLEAR).</summary>
        IcoClear = 230,

        /// <summary>A character that is not a key (a packet of Unicode) (OSKEY_PACKET).</summary>
        Packet = 231,

        /// <summary>Reset on Nokia/Ericsson keyboards (OSKEY_OEM_RESET).</summary>
        OemReset = 233,

        /// <summary>Jump on Nokia/Ericsson keyboards (OSKEY_OEM_JUMP).</summary>
        OemJump = 234,

        /// <summary>PA1 on Nokia/Ericsson keyboards (OSKEY_OEM_PA1).</summary>
        OemPa1 = 235,

        /// <summary>PA2 on Nokia/Ericsson keyboards (OSKEY_OEM_PA2).</summary>
        OemPa2 = 236,

        /// <summary>PA3 on Nokia/Ericsson keyboards (OSKEY_OEM_PA3).</summary>
        OemPa3 = 237,

        /// <summary>WsCtrl on Nokia/Ericsson keyboards (OSKEY_OEM_WSCTRL).</summary>
        OemWsCtrl = 238,

        /// <summary>CuSel on Nokia/Ericsson keyboards (OSKEY_OEM_CUSEL).</summary>
        OemCuSel = 239,

        /// <summary>Attn on Nokia/Ericsson keyboards (OSKEY_OEM_ATTN).</summary>
        OemAttn = 240,

        /// <summary>Finish on Nokia/Ericsson keyboards (OSKEY_OEM_FINISH).</summary>
        OemFinish = 241,

        /// <summary>Copy on Nokia/Ericsson keyboards (OSKEY_OEM_COPY).</summary>
        OemCopy = 242,

        /// <summary>Auto on Nokia/Ericsson keyboards (OSKEY_OEM_AUTO).</summary>
        OemAuto = 243,

        /// <summary>Enlw on Nokia/Ericsson keyboards (OSKEY_OEM_ENLW).</summary>
        OemEnlw = 244,

        /// <summary>BackTab on Nokia/Ericsson keyboards (OSKEY_OEM_BACKTAB).</summary>
        OemBackTab = 245,

        /// <summary>Attn (OSKEY_ATTN).</summary>
        Attn = 246,

        /// <summary>CrSel (OSKEY_CRSEL).</summary>
        CrSel = 247,

        /// <summary>ExSel (OSKEY_EXSEL).</summary>
        ExSel = 248,

        /// <summary>Erase EOF (OSKEY_EREOF).</summary>
        EraseEof = 249,

        /// <summary>Play (OSKEY_PLAY).</summary>
        Play = 250,

        /// <summary>Zoom (OSKEY_ZOOM).</summary>
        Zoom = 251,

        /// <summary>A reserved key (OSKEY_NONAME).</summary>
        NoName = 252,

        /// <summary>PA1 (OSKEY_PA1).</summary>
        Pa1 = 253,

        /// <summary>Clear on OEM keyboards (OSKEY_OEM_CLEAR).</summary>
        OemClear = 254,
    }
}
