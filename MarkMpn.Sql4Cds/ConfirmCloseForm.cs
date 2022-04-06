﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MarkMpn.Sql4Cds
{
    public partial class ConfirmCloseForm : Form
    {
        public ConfirmCloseForm(string[] files, bool cancelable)
        {
            InitializeComponent();

            listBox.Items.Clear();
            listBox.Items.AddRange(files);

            cancelButton.Enabled = cancelable;
        }
    }
}