using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AutoPrinter
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        private void txtPass_Enter(object sender, EventArgs e)
        {
            txtPass.PasswordChar = '\0';
        }

        private void txtPass_Leave(object sender, EventArgs e)
        {
            txtPass.PasswordChar = '*';
        }

        private void BtnLoadPrinterList_Click(object sender, EventArgs e)
        {
            cbPrinterList.Items.Clear();
            foreach (string item in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            {
                cbPrinterList.Items.Add(item);
            }
            if(cbPrinterList.Items.Count>0)
                cbPrinterList.SelectedIndex = 1;
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            btnLoadPrinterList.PerformClick();
            try
            {
                var settings = File.ReadAllLines("setting.txt");
                txtEmail.Text = settings[0];
                txtPass.Text = settings[1];
                cbPrinterList.Text = settings[2];
                numericUpDown1.Text = settings[3];
            }
            catch
            {

            }
            
            try
            {

                txtMails.Lines = File.ReadAllLines("mail.txt");
            }
            catch
            {

            }
        }
        void CheckMailPrinter()
        {
            try
            {
                using (var client = new ImapClient())
                {
                    client.Connect("imap.gmail.com", 993, true);
                    client.AuthenticationMechanisms.Remove("XOAUTH2");

                    client.Authenticate(txtEmail.Text, txtPass.Text);
                    client.Inbox.Open(FolderAccess.ReadWrite);
                    var FolderTrash = client.GetFolder(SpecialFolder.Trash);
                    var mail = client.Inbox.Search(SearchQuery.All);
                    
                    foreach (var ids in mail)
                    {
                        Application.DoEvents();
                        var message = client.Inbox.GetMessage(ids);
                        if ((txtMails.Lines.Length>0 && !txtMails.Lines.Contains(message.Sender.Address))||!message.Subject.StartsWith("[AutoMailPrinter]")) continue;
                        label6.Text = "Thực hiện in mail: " + message.Subject;
                        client.Inbox.MoveTo(ids, FolderTrash);
                        if (message.Attachments.Count() > 0)
                        {
                            foreach (var item in message.Attachments)
                            {
                                string pathFile = DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + "-" + item.ContentDisposition.FileName;
                                using (var stream = File.Create(pathFile))
                                {
                                    if (item is MessagePart)
                                    {
                                        var part = (MessagePart)item;

                                        part.Message.WriteTo(stream);
                                    }
                                    else
                                    {
                                        var part = (MimePart)item;

                                        part.Content.DecodeTo(stream);
                                    }
                                }
                                Application.DoEvents();
                                PrintPdf(pathFile, message.GetTextBody(MimeKit.Text.TextFormat.Text)); 
                                Application.DoEvents();
                                try
                                {
                                    File.Delete(pathFile);
                                }
                                catch
                                {

                                }
                            }
                        }
                        Console.WriteLine("Subject: {0}", message.Subject);
                    }

                    client.Disconnect(true);
                }
            }catch(Exception ex)
            {
                File.WriteAllText("err.txt", ex.Message);
            }
            
            int time = (int)numericUpDown1.Value;
            time = time * 60;
            while (true)
            {
                Application.DoEvents();
                if (!flgStart) return;
                    time--;
                if (time <= 0) break;
                Thread.Sleep(1000);
                label6.Text = "Chờ "+time+" giây";
            }
            CheckMailPrinter();
        }
        private void PrintPdf(string pdfPath,string config)
        {
            using (PdfDocument document = PdfDocument.Load(pdfPath))
            {
                using(PrintDocument printDocument = document.CreatePrintDocument())
                {
                    printDocument.PrinterSettings = GetPrinterSettings(config);
                    printDocument.Print();
                }
            }
        }
        private PrinterSettings GetPrinterSettings(string configs)
        {
            var PrinterSettings = new PrinterSettings();
            PrinterSettings.PrinterName = cbPrinterList.Text;
            var configList = configs.Split('\n');
           foreach(var item in configList)
            {
                var value = item.Substring(item.IndexOf(":") + 1);
                //Trang: 1 - 10 (Full)
                if (item.IndexOf("Trang:") >-1)
                {
                    if (value.IndexOf('-') > -1)
                    {
                        var value_start = value.Substring(0,value.IndexOf("-"));
                        var value_end = value.Substring(value.IndexOf("-") + 1);
                        PrinterSettings.FromPage= ParseInt(value_start, 0);
                        PrinterSettings.ToPage = ParseInt(value_end, 0);
                    }
                }
                //Số Lượng: 5(Mặc định 1)
                if (item.IndexOf("Số Lượng:") > -1)
                {
                    PrinterSettings.Copies = (short)ParseInt(value, 1);
                }
                //Kiểu: 1 (1-Dọc, 2- Ngang)
                if (item.IndexOf("Kiểu:") > -1)
                {
                    //var varInt = (short)ParseInt(value, 1); ;
                    //PrinterSettings.Collate = varInt == 1;

                }
                //Loại: 1(1 - 1 mặt, 2 - 2 mặt dọc, 3 - 2 mặt ngang)
                if (item.IndexOf("Loại:") > -1)
                {
                    var varInt = (short)ParseInt(value, 1); ;
                    if (varInt == 2)
                    {
                        PrinterSettings.Duplex = Duplex.Vertical;
                    }
                    if (varInt == 3)
                    {
                        PrinterSettings.Duplex = Duplex.Horizontal;
                    }
                }
            }
            return PrinterSettings;
        }
        private int ParseInt(string num,int @default=0)
        {
            try
            {
                return int.Parse(num.Trim());
            }catch
            {
                return @default;
            }
        }
        private bool flgStart = false;

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (txtEmail.Text.Trim() == "")
            {
                MessageBox.Show("Vui lòng nhập email");
                return;
            }
            if (txtPass.Text.Trim() == "")
            {
                MessageBox.Show("Vui lòng nhập pass của email");
                return;
            }
            if (cbPrinterList.Text.Trim() == "")
            {
                MessageBox.Show("Vui lòng chọn máy in");
                return;
            }
            groupBox2.Enabled = flgStart;
            txtEmail.Enabled = flgStart;
            txtPass.Enabled = flgStart;
            cbPrinterList.Enabled = flgStart;
            numericUpDown1.Enabled = flgStart;
            btnLoadPrinterList.Enabled = flgStart;
            Application.DoEvents();
            if (flgStart)
            {
                label6.Text = "";
                btnStart.Text = "Bắt đầu chạy";
                Application.DoEvents();
                flgStart = false;
            }
            else
            {
                label6.Text = "";
                flgStart = true;
                btnStart.Text = "Dừng lại";
                label6.Text = "Bắt đầu đọc mail"; 
                Application.DoEvents();
                CheckMailPrinter();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (txtEmail.Text.Trim() == "")
            {
                MessageBox.Show("Vui lòng nhập email");
                return;
            }
            if (txtPass.Text.Trim() == "")
            {
                MessageBox.Show("Vui lòng nhập pass của email");
                return;
            }
            if (cbPrinterList.Text.Trim() == "")
            {
                MessageBox.Show("Vui lòng chọn máy in");
                return;
            }

            List<string> settings = new List<string>();
            settings.Add(txtEmail.Text);
            settings.Add(txtPass.Text);
            settings.Add(cbPrinterList.Text);
            settings.Add(numericUpDown1.Text);
            File.WriteAllLines("setting.txt", settings);
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                File.WriteAllLines("mail.txt", txtMails.Lines);
            }
            catch
            {

            }
        }
    }
}
