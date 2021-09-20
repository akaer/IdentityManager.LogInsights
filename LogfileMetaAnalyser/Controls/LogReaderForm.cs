﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogfileMetaAnalyser.LogReader;

namespace LogfileMetaAnalyser.Controls
{
    public partial class LogReaderForm : Form
    {
        public LogReaderForm()
        {
            InitializeComponent();
        }


        public string ConnectionString
        {
            get { return SelectedProvider.ConnectionString; }
        }


        public ILogReader ConnectToReader()
        {
            return SelectedProvider.ConnectToReader();
        }

        private LogReaderControl SelectedProvider
        {
            get
            {
                if (tabControl.SelectedIndex == 0)
                    return ctlLogFile;

                if (tabControl.SelectedIndex == 1)
                    return ctlAppInsights;

                throw new Exception("Invalid provider selection.");
            }
        }
    }
}