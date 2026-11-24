// Orcamento.exe - abre o app em uma janela propria (sem barra do navegador), usando o Edge ou o Chrome ja instalados.
// Os dados ficam em %LOCALAPPDATA%\Orcamento\perfil (separados da pasta do programa), criptografados pelo proprio app.
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

static class Program
{
    static string Achar()
    {
        string[] chaves = { "msedge.exe", "chrome.exe" };
        foreach (string exe in chaves)
        {
            foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                try
                {
                    using (RegistryKey k = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
                        .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe))
                    {
                        string p = k == null ? null : (k.GetValue(null) as string);
                        if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                    }
                }
                catch { }
            }
        }
        string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        string pf = Environment.GetEnvironmentVariable("ProgramFiles");
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        string[] candidatos = {
            Path.Combine(pf86 ?? "", @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf ?? "", @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf ?? "", @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86 ?? "", @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(local ?? "", @"Google\Chrome\Application\chrome.exe")
        };
        foreach (string c in candidatos) if (File.Exists(c)) return c;
        return null;
    }

    [STAThread]
    static void Main(string[] args)
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string index = Path.Combine(dir, "index.html");
        if (!File.Exists(index))
        {
            MessageBox.Show("Não encontrei o arquivo index.html ao lado deste programa.\nMantenha a pasta do aplicativo completa.", "Orçamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string navegador = Achar();
        if (navegador == null)
        {
            MessageBox.Show("Este programa precisa do Microsoft Edge (já vem no Windows) ou do Google Chrome.", "Orçamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string perfil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orcamento", "perfil");
        Directory.CreateDirectory(perfil);
        string url = new Uri(index).AbsoluteUri;
        string argumentos = "--app=\"" + url + "\" --user-data-dir=\"" + perfil + "\" --no-first-run --no-default-browser-check --start-maximized --disable-sync --disable-features=Translate,msEdgeShopping,msEdgeSyncAutoSignIn";
        try
        {
            Process.Start(new ProcessStartInfo(navegador, argumentos) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível abrir o aplicativo:\n" + ex.Message, "Orçamento", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
