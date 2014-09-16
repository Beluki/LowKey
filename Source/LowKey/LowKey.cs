
// LowKey.
// A simple low-level keyboard hooker for .NET.


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;


namespace LowKey
{
    /// <summary>
    /// All exceptions that the KeyboardHook raises are of this type.
    /// </summary>
    public class KeyboardHookException : Exception
    {
        public KeyboardHookException(String message) : base(message)
        {
        }
    }

    /// <summary>
    /// LowKey keyboard hooker.
    /// </summary>
    public static class KeyboardHook
    {
        ///
        /// Helpers
        ///

        /// <summary>
        /// Retrieve the last-error as a readable string.
        /// </summary>
        private static String LastWin32Error()
        {
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }

        /// <summary>
        /// Determine whether the alt key modifier is currently pressed.
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

        ///
        /// Hooker
        ///

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
        /// All the hooked keys and their modifiers.
        /// Base key vkCode -> active modifiers (bitwise combination) for that key.
        /// </summary>
        private static Dictionary<Keys, HashSet<Keys>> hookedKeys =
            new Dictionary<Keys, HashSet<Keys>>();


        /// <summary>
        /// Add a key to the hooker.
        /// </summary>
        /// <param name="key">
        /// Base key that should be pressed to fire an event.
        /// </param>
        /// <param name="modifiers">
        /// Modifier keys.
        /// </param>
        public static void AddKey(Keys key, Keys modifiers = Keys.None)
        {
            // base key already present:
            if (hookedKeys.ContainsKey(key))
            {
                HashSet<Keys> current_modifiers = hookedKeys[key];
                if (current_modifiers.Contains(modifiers))
                {
                    throw new KeyboardHookException("Key already hooked.");
                }

                current_modifiers.Add(modifiers);
            }

            // new base key + modifiers:
            else
            {
                HashSet<Keys> new_modifiers = new HashSet<Keys>();
                new_modifiers.Add(modifiers);
                hookedKeys.Add(key, new_modifiers);
            }
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

                    // when SetWindowsHookEx, the result is NULL:
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
            if (UnhookWindowsHookEx(hookID))
            {
                hookID = IntPtr.Zero;
            }
            else
            {
                throw new KeyboardHookException(
                    "UnhookWindowsHookEx() failed: " + LastWin32Error()
                );
            }
        }

        /// <summary>
        /// Actual callback that intercepts key presses.
        /// </summary>
        private static IntPtr Callback(Int32 nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                Int32 message = wParam.ToInt32();
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    // the vkCode is the first KBDLLHOOKSTRUCT member:
                    Keys key = (Keys) Marshal.ReadInt32(lParam);

                    if (hookedKeys.ContainsKey(key))
                    {
                        Debug.WriteLine("Base key present");

                        // determine current modifiers pressed:
                        Keys currentModifiers = Keys.None;

                        if (IsAltPressed)
                        {
                            Debug.WriteLine("Alt pressed");
                            currentModifiers |= Keys.Alt;
                        }

                        if (IsControlPressed)
                        {
                            Debug.WriteLine("Control pressed");
                            currentModifiers |= Keys.Control;
                        }

                        if (IsShiftPressed)
                        {
                            Debug.WriteLine("Shift pressed");
                            currentModifiers |= Keys.Shift;
                        }

                        // look for a match:
                        HashSet<Keys> modifiers = hookedKeys[key];
                        if (modifiers.Contains(currentModifiers))
                        {
                            MessageBox.Show("Yes!");
                        }
                    }
                }
            }

            // send the message to the next hook:
            return CallNextHookEx(hookID, nCode, wParam, lParam);
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

        ///
        /// Testing
        ///

        public static void Main(String[] args)
        {
            try
            {
                KeyboardHook.Hook();
                KeyboardHook.AddKey(Keys.X, Keys.Control | Keys.Alt);
            }
            catch (KeyboardHookException e)
            {
                Console.WriteLine(e.Message);
            }

            Application.Run();
        }
    }
}

