//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Windows.Forms;

namespace Microsoft.Azure.DocumentDBStudio
{
    static class Program
    {
        private static MainForm _mainForm;

        public static MainForm GetMain()
        {
            return _mainForm;
        }

        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _mainForm = new MainForm();
            Application.Run(_mainForm);
        }
    }
}