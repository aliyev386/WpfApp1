using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
namespace XORFileEncryptDecrypt
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
                txtFile.Text = ofd.FileName;
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFile.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("File və password daxil edin!");
                return;
            }

            cts = new CancellationTokenSource();
            string filePath = txtFile.Text;
            string password = txtPassword.Text;
            bool encrypt = rbEncrypt.IsChecked == true;

            try
            {
                await Task.Run(() => XORFile(filePath, password, encrypt, cts.Token));
                MessageBox.Show("Proses bitdi!");
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("İş ləğv edildi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xəta: " + ex.Message);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        private void XORFile(string filePath, string password, bool encrypt, CancellationToken token)
        {
            byte[] pwdBytes = System.Text.Encoding.UTF8.GetBytes(password);
            string tempFile = filePath + ".tmp";

            using (FileStream fsIn = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (FileStream fsOut = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                long length = fsIn.Length;
                byte[] buffer = new byte[4096];
                int bytesRead;
                long totalRead = 0;

                while ((bytesRead = fsIn.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();

                    for (int i = 0; i < bytesRead; i++)
                    {
                        buffer[i] ^= pwdBytes[i % pwdBytes.Length]; // XOR əməliyyatı
                    }

                    fsOut.Write(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = (totalRead * 100) / length;
                    });
                }
            }

            // Orijinal faylı əvəz et
            File.Delete(filePath);
            File.Move(tempFile, filePath);

            Dispatcher.Invoke(() => progressBar.Value = 0);
        }
    }
}
