using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace CheckSiteUpdate
{
    public class ConsoleModeControl
    {
        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(
            IntPtr hConsoleHandle,
            out int lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(
            IntPtr hConsoleHandle,
            int ioMode);

        /// <summary>
        /// This flag enables the user to use the mouse to select and edit text. To enable
        /// this option, you must also set the ExtendedFlags flag.
        /// </summary>
        private const int QuickEditMode = 64;

        // ExtendedFlags must be combined with
        // InsertMode and QuickEditMode when setting
        /// <summary>
        /// ExtendedFlags must be enabled in order to enable InsertMode or QuickEditMode.
        /// </summary>
        private const int ExtendedFlags = 128;

        private const int STD_INPUT_HANDLE = -10;

        private const int INVALID_HANDLE_VALUE = -1;

        private readonly IntPtr ConHandle = IntPtr.Zero;

        private readonly int OriginalCosoleMode;      


        public ConsoleModeControl()
        {
            ConHandle = GetStdHandle(STD_INPUT_HANDLE);
            if (ConHandle.ToInt32() == INVALID_HANDLE_VALUE)
            {
                throw new InvalidOperationException("Failed to get handle.");
            }
            int mode;

            if (!GetConsoleMode(ConHandle, out mode))
            {
                throw new Exception();
            }

            OriginalCosoleMode = mode;
        }

        public void DisableQuickEdit()
        {
            if (ConHandle == IntPtr.Zero || ConHandle.ToInt32() == INVALID_HANDLE_VALUE)
                return;

            int mode = 0;
            if (!GetConsoleMode(ConHandle, out mode))
            {
                // error getting the console mode. Exit.
                return;
            }

            mode = mode & ~(QuickEditMode | ExtendedFlags);

            if (!SetConsoleMode(ConHandle, mode))
            {
                // error setting console mode.
            }
        }

        public void EnableQuickEdit()
        {
            if (ConHandle == IntPtr.Zero || ConHandle.ToInt32() == INVALID_HANDLE_VALUE)
                return;

            int mode = 0;

            if (!GetConsoleMode(ConHandle, out mode))
            {
                // error getting the console mode. Exit.
                return;
            }

            mode = mode | (QuickEditMode | ExtendedFlags);

            if (!SetConsoleMode(ConHandle, mode))
            {
                // error setting console mode.
            }
        }

        public void RestoreOriginalMode()
        {
            if (ConHandle == IntPtr.Zero || ConHandle.ToInt32() == INVALID_HANDLE_VALUE)
                return;

            if (!SetConsoleMode(ConHandle, OriginalCosoleMode))
            {
                // error setting console mode.
            }
        }
    }
}
