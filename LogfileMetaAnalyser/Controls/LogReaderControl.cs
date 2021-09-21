﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogfileMetaAnalyser.Properties;
using LogfileMetaAnalyser.LogReader;

namespace LogfileMetaAnalyser.Controls
{
    public partial class LogReaderControl : UserControl
    {
        private bool _isValid;

        public event EventHandler IsValidChanged;

        public LogReaderControl()
        {
            InitializeComponent();
        }

        public virtual string ConnectionString
        {
            get { return GetConnectionString(); }
        }

        protected virtual string GetConnectionString()
        {
            return string.Empty;
        }

        public bool IsValid
        {
            get { return _isValid; }

            set
            {
                if (value == _isValid)
                    return;

                _isValid = value;

                OnValidChanged();
            }
        }

        public virtual void CheckValid()
        {
            IsValid = OnCheckValid();
        }

        protected virtual bool OnCheckValid()
        {
            return true;
        }


        protected virtual void OnValidChanged()
        {
            IsValidChanged?.Invoke(this, EventArgs.Empty);
        }

        public virtual ILogReader CreateReader()
        {
            return null;
        }

        public virtual ILogReader ConnectToReader()
        {
            throw new NotImplementedException();
        }
    }
}
