using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModelDetectionPlugin {
    public partial class SystemNameForm : Form {
        public SystemNameForm() {
            InitializeComponent();
        }

        string m_systemName;
        public string SystemName {
            get { return m_systemName; }
        }

        private void button1_Click(object sender, EventArgs e) {
            m_systemName = textBox1.Text.ToString();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void textBox1_TextChanged(object sender, EventArgs e) {
            m_systemName = textBox1.Text.ToString();
        }
    }
}
