using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace PEFile
{
    public partial class FileForm : Form
    {
        private PEFile PEFile = null;

        public FileForm()
        {
            InitializeComponent();
        }

        private void FileForm_Load(object sender, EventArgs e)
        {
            TxtSummary.Width = this.Width - 40;
            TxtSummary.Height = this.Height - 60;
        }

        private void FileForm_Resize(object sender, EventArgs e)
        {
            TxtSummary.Width = this.Width - 40;
            TxtSummary.Height = this.Height - 60;
        }

        public void OpenPEFile(string filename)
        {
            this.PEFile = new PEFile(filename);
            
        }

        public void DrawSummary()
        {
        
        }

        public void Export(string output)
        {
            if (PEFile != null)
            {
                PEFile.Export(output);
                MessageBox.Show("输出完成！");
            }
            else
            {
                MessageBox.Show("请先打开需要输出的文件！");
            }
        }
    }
}
