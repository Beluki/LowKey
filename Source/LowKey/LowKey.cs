
// LowKey.
// A simple low-level keyboard hooker for .NET.


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace LowKey
{
    /// <summary>
    /// All the exceptions that KeyboardHook raises are of this type.
    /// </summary>
    public class KeyboardHookException : Exception
    {
        public KeyboardHookException(String message) : base(message)
        {
        }
    }

    /// <summary>
    /// Gives information about a hotkey when an event is fired.
    /// </summary>
    public class KeyboardHookEventArgs : EventArgs
    {
        public Keys Key;
        public Keys Modifiers;
        public Boolean Handled;
        public String Name;

        /// <summary>
        /// Information about the current hotkey pressed.
        /// </summary>
        /// <param name="key">
        /// Base key that was pressed when the event was fired.
        /// </param>
        /// <param name="modifiers">
        /// Modifiers pressed.
        /// </param>
        /// <param name="name">
        /// Hotkey name or null when no name.
        /// </param>
        /// <param name="handled">
        /// When False, the keypress will be passed to other applications.
        /// True by default.
        /// </param>
        public KeyboardHookEventArgs(Keys key, Keys modifiers, String name, Boolean handled = true)
        {
            Key = key;
            Modifiers = modifiers;
            Name = name;
            Handled = handled;
        }
    }

    /// <summary>
    /// The LowKey keyboard hooker.
    /// </summary>
    public static class KeyboardHook
    {
        ///
        /// Events
        ///

        public static event EventHandler<KeyboardHookEventArgs> HotkeyUp = null;
        public static event EventHandler<KeyboardHookEventArgs> HotkeyDown = null;

        ///
        /// Helpers
        ///

        /// <summary>
        /// Retrieve the last Windows error as a readable string.
        /// </summary>
        private static String LastWin32Error()
        {
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }

        /// <summary>
        /// Determine whether the alt key modifier is pressed right now.
        /// </summary>
        private static Boolean IsAltPressed
        {
            get { return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0; }
        }

        /// <summary>
        /// Determine whether the control key modifier is currently pressed.
        /// </summary>
        private static Boolean IsControlPressed
        {
            get { return (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0; }
        }

        /// <summary>
        /// Determine whether the shift key modifier is currently pressed.
        /// </summary>
        private static Boolean IsShiftPressed
        {
            get { return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0; }
        }

        /// <summary>
        /// Determine which modifiers (control, alt, shift)
        /// are currently pressed.
        /// </summary>
        private static Keys PressedModifiers
        {
            get
            {
                Keys modifiers = Keys.None;

                if (IsAltPressed)
                    modifiers |= Keys.Alt;

                if (IsControlPressed)
                    modifiers |= Keys.Control;

                if (IsShiftPressed)
                    modifiers |= Keys.Shift;

                return modifiers;
            }
        }

        ///
        /// Hooker
        ///

        private struct Hotkey
        {
            public Keys Key;
            public Keys Modifiers;

            /// <summary>
            /// Represents a hotkey as a combination of a base key
            /// and additional modifiers.
            /// </summary>
            /// <param name="key">
            /// Base key.
            /// </param>
            /// <param name="modifiers">
            /// A bitwise combination of additional modifiers
            /// e.g: Keys.Control | Keys.Alt.
            /// </param>
            public Hotkey(Keys key, Keys modifiers = Keys.None)
            {
                Key = key;
                Modifiers = modifiers;
            }
        }

        /// <summary>
        /// All the hooked hotkeys.
        /// </summary>
        private static HashSet<Hotkey> hotkeys = new HashSet<Hotkey>();

        /// <summary>
        /// Virtual key codes for all the hooked hotkeys.
        /// </summary>
        private static HashSet<Int32> hotkeysVkCodes = new HashSet<Int32>();

        /// <summary>
        /// A map from hotkeys to names.
        /// </summary>
        private static Dictionary<Hotkey, String> hotkeyToName =
            new Dictionary<Hotkey, String>();

        /// <summary>
        /// A map from names to hotkeys.
        /// </summary>
        private static Dictionary<String, Hotkey> nameToHotkey =
            new Dictionary<String, Hotkey>();

        /// <summary>
        /// Hook ID.
        /// Will be IntPtr.Zero when not currently hooked.
        /// </summary>
        private static IntPtr hookID = IntPtr.Zero;

        /// <summary>
        /// Needed to avoid the delegate being garbage-collected.
        /// </summary>
        private static HOOKPROC hookedCallback = Callback;

        /// <summary>
        /// Add a hotkey to the hooker.
        /// </summary>
        /// <param name="key">
        /// Base key that should be pressed to fire an event.
        /// </param>
        /// <param name="modifiers">
        /// A bitwise combination of additional modifiers
        /// e.g: Keys.Control | Keys.Alt.
        /// </param>
        private static Hotkey InternalAddHotkey(Keys key, Keys modifiers)
        {
            Hotkey hotkey = new Hotkey(key, modifiers);
            Int32 vkCode = (Int32) key;

            // provide a more detailed error message in this case:
            if (hotkeyToName.ContainsKey(hotkey))
            {
                throw new KeyboardHookException(
                    String.Format(
                        "A named key: {} exists with the same key/modifiers: ",
                        hotkeyToName[hotkey]
                    )
                );
            }

            if (hotkeys.Contains(hotkey))
            {
                throw new KeyboardHookException("Hotkey already hooked.");
            }

            hotkeys.Add(hotkey);
            hotkeysVkCodes.Add(vkCode);
            return hotkey;
        }

        /// <summary>
        /// Add a hotkey to the hooker.
        /// </summary>
        /// <param name="key">
        /// Base key that should be pressed to fire an event.
        /// </param>
        /// <param name="modifiers">
        /// A bitwise combination of additional modifiers
        /// e.g: Keys.Control | Keys.Alt.
        /// </param>
        public static void AddHotkey(Keys key, Keys modifiers = Keys.None)
        {
            InternalAddHotkey(key, modifiers);
        }

        /// <summary>
        /// Add a named hotkey to the hooker.
        /// </summary>
        /// <param name="name">
        /// An identifier for the hotkey.
        /// </param>
        /// <param name="key">
        /// Base key that should be pressed to fire an event.
        /// </param>
        /// <param name="modifiers">
        /// A bitwise combination of additional modifiers
        /// e.g: Keys.Control | Keys.Alt.
        /// </param>
        public static void AddHotkey(String name, Keys key, Keys modifiers = Keys.None)
        {
            // check the name:
            if (name == null)
            {
                throw new KeyboardHookException("Invalid hotkey name.");
            }

            if (nameToHotkey.ContainsKey(name))
            {
                throw new KeyboardHookException("Hotkey name already taken.");
            }

            Hotkey hotkey = InternalAddHotkey(key, modifiers);
            nameToHotkey.Add(name, hotkey);
            hotkeyToName.Add(hotkey, name);
        }

        /// <summary>
        /// Remove a hotkey from the hooker.
        /// </summary>
        /// <param name="key">
        /// Base key.
        /// </param>
        /// <param name="modifiers">
        /// A bitwise combination of additional modifiers
        /// e.g: Keys.Control | Keys.Alt.
        /// </param>
        public static void RemoveHotkey(Keys key, Keys modifiers = Keys.None)
        {
            Hotkey hotkey = new Hotkey(key, modifiers);
            Int32 vkCode = (Int32) key;

            if (!hotkeys.Contains(hotkey))
            {
                throw new KeyboardHookException("Hotkey not currently hooked.");
            }

            hotkeys.Remove(hotkey);
            hotkeysVkCodes.Remove(vkCode);

            // remove from both lookup dicts if present too:
            if (hotkeyToName.ContainsKey(hotkey))
            {
                String name = hotkeyToName[hotkey];
                hotkeyToName.Remove(hotkey);
                nameToHotkey.Remove(name);
            }
        }

        /// <summary>
        /// Remove a named hotkey from the hooker.
        /// </summary>
        /// <param name="name">
        /// Hotkey name that was specified when calling AddHotkey().
        /// </param>
        public static void RemoveHotkey(String name)
        {
            // check the name:
            if (name == null)
            {
                throw new KeyboardHookException("Invalid hotkey name.");
            }

            if (!nameToHotkey.ContainsKey(name))
            {
                throw new KeyboardHookException("Unknown hotkey name.");
            }

            Hotkey hotkey = nameToHotkey[name];
            RemoveHotkey(hotkey.Key, hotkey.Modifiers);
        }


        /// <summary>
        /// Start looking for key presses.
        /// </summary>
        public static void Hook()
        {
            // don't hook twice:
            if (hookID != IntPtr.Zero)
            {
                throw new KeyboardHookException(
                    "The keyboard hook is already active. Call Unhook() first."
                );
            }

            using (Process process = Process.GetCurrentProcess())
            {
                using (ProcessModule module = process.MainModule)
                {
                    IntPtr hMod = GetModuleHandle(module.ModuleName);
                    hookID = SetWindowsHookEx(WH_KEYBOARD_LL, hookedCallback, hMod, 0);

                    // when SetWindowsHookEx fails, the result is NULL:
                    if (hookID == IntPtr.Zero)
                    {
                        throw new KeyboardHookException(
                            "SetWindowsHookEx() failed: " + LastWin32Error()
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Stop looking for key presses.
        /// </summary>
        public static void Unhook()
        {
            // not hooked:
            if (hookID == IntPtr.Zero)
            {
                throw new KeyboardHookException(
                    "The keyboard hook is not currently active. Call Hook() first."
                );
            }

            // when UnhookWindowsHookEx fails, the result is false:
            if (!UnhookWindowsHookEx(hookID))
            {
                throw new KeyboardHookException(
                    "UnhookWindowsHookEx() failed: " + LastWin32Error()
                );
            }

            hookID = IntPtr.Zero;
        }

        /// <summary>
        /// Actual callback that intercepts key presses.
        /// </summary>
        private static IntPtr Callback(Int32 nCode, IntPtr wParam, IntPtr lParam)
        {
            Boolean handled = false;
            if (nCode >= 0)
            {
                Int32 msg = wParam.ToInt32();

                if ((msg == WM_KEYUP) || (msg == WM_SYSKEYUP)
                    || (msg == WM_KEYDOWN) || (msg == WM_SYSKEYDOWN))
                {
                    // the vkCode is the first KBDLLHOOKSTRUCT member:
                    Int32 vkCode = Marshal.ReadInt32(lParam);

                    if (hotkeysVkCodes.Contains(vkCode))
                    {
                        Keys key = (Keys) vkCode;
                        Keys modifiers = PressedModifiers;
                        Hotkey hotkey = new Hotkey(key, modifiers);

                        // look for a match:
                        if (hotkeys.Contains(hotkey))
                        {
                            String name = null;
                            hotkeyToName.TryGetValue(hotkey, out name);

                            KeyboardHookEventArgs args = new KeyboardHookEventArgs(
                                key,
                                modifiers,
                                name,
                                true
                            );

                            if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                            {
                                if (HotkeyUp != null)
                                {
                                    HotkeyUp(null, args);
                                    handled = args.Handled;
                                }
                            }
                            else
                            {
                                if (HotkeyDown != null)
                                {
                                    HotkeyDown(null, args);
                                    handled = args.Handled;
                                }
                            }
                        }
                    }
                }
            }

            // return a dummy value other than 0
            // when the message was handled:
            if (handled)
            {
                return new IntPtr(1);
            }
            else
            {
                return CallNextHookEx(hookID, nCode, wParam, lParam);
            }
        }

        ///
        /// Private Windows API declarations
        ///

        private const Int32 VK_SHIFT = 0x10;
        private const Int32 VK_CONTROL = 0x11;
        private const Int32 VK_MENU = 0x12;

        private const Int32 WH_KEYBOARD_LL = 13;

        private const Int32 WM_SYSKEYDOWN = 0x0104;
        private const Int32 WM_SYSKEYUP = 0x0105;
        private const Int32 WM_KEYDOWN = 0x0100;
        private const Int32 WM_KEYUP = 0x0101;

        private delegate IntPtr HOOKPROC(Int32 nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(Int32 idHook, HOOKPROC lpfn, IntPtr hMod, UInt32 dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern Boolean UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, Int32 nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(String lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Int16 GetAsyncKeyState(Int32 vKey);
    }
}

