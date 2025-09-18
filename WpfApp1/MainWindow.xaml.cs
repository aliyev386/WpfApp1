using Microsoft.Win32;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? cts;
        private bool UseAes = true; // true = AES, false = XOR

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
                txtFile.Text = ofd.FileName;
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFile.Text) || !File.Exists(txtFile.Text))
            {
                MessageBox.Show("Fayl seçin.");
                return;
            }

            if (string.IsNullOrEmpty(txtPassword.Password))
            {
                MessageBox.Show("Zəhmət olmasa şifrə daxil edin.");
                return;
            }

            string password = txtPassword.Password;

            btnStart.IsEnabled = false;
            btnCancel.IsEnabled = true;
            progressBar.Value = 0;

            cts = new CancellationTokenSource();
            string filePath = txtFile.Text;
            bool encrypt = rbEncrypt.IsChecked == true;

            try
            {
                if (UseAes)
                {
                    await Task.Run(() =>
                    {
                        if (encrypt) AesFileEncrypt(filePath, password, cts.Token);
                        else AesFileDecrypt(filePath, password, cts.Token);
                    });
                }
                else
                {
                    await Task.Run(() => XORFileWithHeader(filePath, password, cts.Token));
                }

                MessageBox.Show("Proses bitdi!");
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("İş ləğv edildi.");
            }
            catch (CryptographicException ex)
            {
                MessageBox.Show("Kripto xətası (parol səhv ola bilər): " + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xəta: " + ex.Message);
            }
            finally
            {
                btnStart.IsEnabled = true;
                btnCancel.IsEnabled = false;
                progressBar.Value = 0;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        // -----------------------------
        // AES Implementation
        // file format: [salt(16)][iv(16)][encrypted data]
        private void AesFileEncrypt(string filePath, string password, CancellationToken token)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] key, iv;

            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256))
            {
                key = deriveBytes.GetBytes(32); // AES-256
                iv = deriveBytes.GetBytes(16);
            }

            string tempFile = filePath + ".tmp"; // müvəqqəti fayl

            using (var fsIn = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var fsOut = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = key;
                aes.IV = iv;

                fsOut.Write(salt, 0, salt.Length);
                fsOut.Write(iv, 0, iv.Length);

                using (var cryptoStream = new CryptoStream(fsOut, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    CopyStreamWithProgress(fsIn, cryptoStream, token);
                }
            }

            // Müvəqqəti faylı orijinal üzərinə yaz
            File.Delete(filePath);
            File.Move(tempFile, filePath);
        }


        private void AesFileDecrypt(string filePath, string password, CancellationToken token)
        {
            string tempFile = filePath + ".tmp";

            using (var fsIn = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] salt = new byte[16];
                byte[] iv = new byte[16];
                fsIn.Read(salt, 0, salt.Length);
                fsIn.Read(iv, 0, iv.Length);

                byte[] key;
                using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256))
                    key = deriveBytes.GetBytes(32);

                using (var fsOut = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                using (var aes = Aes.Create())
                using (var cryptoStream = new CryptoStream(fsOut, aes.CreateDecryptor(key, iv), CryptoStreamMode.Write))
                {
                    CopyStreamWithProgress(fsIn, cryptoStream, token); // fsIn → cryptoStream yazılır
                }
            }

            File.Delete(filePath);
            File.Move(tempFile, filePath);
        }



        // -----------------------------
        // XOR Implementation
        private void XORFileWithHeader(string filePath, string password, CancellationToken token)
        {
            byte[] key = Encoding.UTF8.GetBytes(password);
            string tempFile = filePath + ".tmp";

            using (var fsIn = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var fsOut = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                int b;
                long total = fsIn.Length;
                long processed = 0;
                while ((b = fsIn.ReadByte()) != -1)
                {
                    token.ThrowIfCancellationRequested();
                    fsOut.WriteByte((byte)(b ^ key[processed % key.Length]));
                    processed++;
                    Dispatcher.Invoke(() => progressBar.Value = (double)processed / total * 100);
                }
            }

            File.Delete(filePath);
            File.Move(tempFile, filePath);
        }


        // -----------------------------
        private void CopyStreamWithProgress(Stream input, Stream output, CancellationToken token)
        {
            byte[] buffer = new byte[81920];
            long total = input.Length - input.Position;
            long processed = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                output.Write(buffer, 0, read);
                processed += read;
                Dispatcher.Invoke(() => progressBar.Value = (double)processed / total * 100);
            }
        }
    }
}
