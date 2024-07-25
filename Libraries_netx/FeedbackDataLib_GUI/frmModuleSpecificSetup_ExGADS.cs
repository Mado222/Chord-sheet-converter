﻿using System.Windows.Forms;
using FeedbackDataLib;
using FeedbackDataLib.Modules;

namespace FeedbackDataLib_GUI
{
    /// <summary>
    /// Just to encapsulate ucModuleSpecificSetup_ExGADS on a form
    /// </summary>
    /// <seealso cref="System.Windows.Forms.Form" />
    public partial class frmModuleSpecificSetup_ExGADS : Form
    {
        //ucModuleSpecificSetup_ExGADS ucModuleSpecificSetup_ExGADS1 = new ucModuleSpecificSetup_ExGADS();
        ucModuleSpecificSetup_ExGADS ucModuleSpecificSetup_ExGADS1 = new ucModuleSpecificSetup_ExGADS();
        public frmModuleSpecificSetup_ExGADS()
        {
            InitializeComponent();
            SuspendLayout();
            ucModuleSpecificSetup_ExGADS1.Dock = System.Windows.Forms.DockStyle.Fill;
            ucModuleSpecificSetup_ExGADS1.Name = "ucModuleSpecificSetup_ExGADS1";
            Controls.Add(ucModuleSpecificSetup_ExGADS1);
            ResumeLayout(false);
        }

        public byte[] GetModuleSpecific()
        {
            return ucModuleSpecificSetup_ExGADS1.GetModuleSpecificInfo();
        }

        public void SetModuleSpecificInfo(CModuleExGADS1292 ModuleInfo)
        {
            ucModuleSpecificSetup_ExGADS1.SetModuleSpecificInfo(ModuleInfo);
        }
    }
}
