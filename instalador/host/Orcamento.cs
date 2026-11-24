// Orcamento.exe (v2) - hospeda o app numa janela propria (WebView2), com icone na bandeja e IA local opcional.
// Compilar: host\compilar-host.bat (csc do Windows, C# 5 - sem interpolacao de strings, sem "?.").
//
//  * Dados em <DADOS>\perfil-wv (padrao DADOS = %LOCALAPPDATA%\Orcamento; use --dados "<pasta>" para trocar).
//  * Na primeira execucao copia (nunca move/apaga) o localStorage/IndexedDB do perfil antigo (Edge --app) para o perfil novo.
//  * Janela com X -> bandeja (como o Discord) se "fecharNaBandeja" estiver ligado (padrao). Ao esconder, o app e BLOQUEADO.
//  * (IA local removida do escopo: __host.iaUrl e sempre null.)

//  * --teste : abre a janela fora da tela e cria um named pipe de controle (so para auto-testes; nunca usado em producao).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

static class Program
{
    public static string Dados, AppDir, Hash;
    public static bool Teste;

    static string Arg(string[] a, string nome)
    {
        for (int i = 0; i < a.Length - 1; i++) if (string.Equals(a[i], nome, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
        return null;
    }

    [STAThread]
    static void Main(string[] args)
    {
        Dados = Arg(args, "--dados");
        if (string.IsNullOrEmpty(Dados)) Dados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orcamento");
        Dados = Path.GetFullPath(Dados);
        AppDir = Arg(args, "--app");
        if (string.IsNullOrEmpty(AppDir)) AppDir = AppDomain.CurrentDomain.BaseDirectory;
        AppDir = Path.GetFullPath(AppDir);
        Teste = Array.IndexOf(args, "--teste") >= 0;
        using (SHA1 sha = SHA1.Create())
            Hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Dados.ToLowerInvariant()))).Replace("-", "").Substring(0, 12);

        if (!File.Exists(Path.Combine(AppDir, "index.html")))
        {
            MessageBox.Show("Não encontrei o arquivo index.html na pasta do aplicativo.\nReinstale o Orçamento.", "Orçamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool nova;
        Mutex mtx = new Mutex(true, "Local\\OrcamentoHost_" + Hash, out nova);
        if (!nova)
        {   // ja esta aberto (talvez na bandeja): pede para ele aparecer
            try { EventWaitHandle.OpenExisting("Local\\OrcamentoHostShow_" + Hash).Set(); } catch { }
            return;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { Log.Escreve("ERRO (UI): " + e.Exception); };
        AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e) { Log.Escreve("ERRO FATAL: " + e.ExceptionObject); };
        try { Application.Run(new HostForm()); }
        finally { GC.KeepAlive(mtx); }
    }
}

class Config
{
    public bool FecharNaBandeja = true;
    public bool AvisoBandeja = false;
    string arq;
    public Config(string dados)
    {
        arq = Path.Combine(dados, "config.json");
        try
        {
            if (File.Exists(arq))
            {
                Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(arq, Encoding.UTF8));
                object v;
                if (d.TryGetValue("fecharNaBandeja", out v) && v is bool) FecharNaBandeja = (bool)v;
                if (d.TryGetValue("avisoBandeja", out v) && v is bool) AvisoBandeja = (bool)v;
            }
        }
        catch { }
    }
    public void Salvar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arq));
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["fecharNaBandeja"] = FecharNaBandeja; d["avisoBandeja"] = AvisoBandeja;
            File.WriteAllText(arq, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
        }
        catch { }
    }
}

static class Log
{
    public static void Escreve(string m)
    {
        try { File.AppendAllText(Path.Combine(Program.Dados, "host.log"), DateTime.Now.ToString("s") + " " + m + "\r\n", Encoding.UTF8); } catch { }
    }
}

class HostForm : Form
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

    WebView2 web = new WebView2();
    NotifyIcon tray;
    Config cfg;
    bool sairDeVez = false;
    EventWaitHandle showEv;
    JavaScriptSerializer js = new JavaScriptSerializer();
    string baseUri;

    public HostForm()
    {
        Directory.CreateDirectory(Program.Dados);
        cfg = new Config(Program.Dados);
        baseUri = new Uri(Program.AppDir.TrimEnd('\\') + "\\").AbsoluteUri;

        Text = "Orçamento comprometido";
        Icon icone = null;
        try { string ico = Path.Combine(Program.AppDir, "icons", "app.ico"); if (File.Exists(ico)) icone = new Icon(ico); } catch { }
        if (icone != null) Icon = icone;
        if (Program.Teste) { StartPosition = FormStartPosition.Manual; Location = new Point(-2600, 0); Size = new Size(1280, 800); }
        else { StartPosition = FormStartPosition.WindowsDefaultLocation; Size = new Size(1280, 800); WindowState = FormWindowState.Maximized; }
        MinimumSize = new Size(900, 600);

        web.Dock = DockStyle.Fill; Controls.Add(web);

        tray = new NotifyIcon();
        tray.Icon = icone ?? SystemIcons.Application; tray.Text = "Orçamento comprometido"; tray.Visible = true;
        ContextMenuStrip m = new ContextMenuStrip();
        ToolStripMenuItem abrir = new ToolStripMenuItem("Abrir Orçamento"); abrir.Font = new Font(abrir.Font, FontStyle.Bold);
        abrir.Click += delegate { Restaurar(); };
        ToolStripMenuItem fechar = new ToolStripMenuItem("Fechar de vez");
        fechar.Click += delegate { SairDeVez(); };
        m.Items.Add(abrir); m.Items.Add(new ToolStripSeparator()); m.Items.Add(fechar);
        tray.ContextMenuStrip = m;
        tray.MouseClick += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Restaurar(); };

        showEv = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\OrcamentoHostShow_" + Program.Hash);
        Thread t = new Thread(delegate () { while (true) { showEv.WaitOne(); try { BeginInvoke(new Action(Restaurar)); } catch { return; } } });
        t.IsBackground = true; t.Start();

        Load += async delegate { await IniciaWeb(); };
        if (Program.Teste) { Thread pt = new Thread(ServidorTeste); pt.IsBackground = true; pt.Start(); }
    }

    /* ---------- migracao do perfil antigo (Edge --app) ---------- */
    static void CopiaDir(string de, string para)
    {
        Directory.CreateDirectory(para);
        foreach (string f in Directory.GetFiles(de))
        {
            if (string.Equals(Path.GetFileName(f), "LOCK", StringComparison.OrdinalIgnoreCase)) continue;
            try { using (FileStream a = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) using (FileStream b = new FileStream(Path.Combine(para, Path.GetFileName(f)), FileMode.Create)) a.CopyTo(b); }
            catch (Exception ex) { Log.Escreve("migracao: " + f + ": " + ex.Message); }
        }
        foreach (string d in Directory.GetDirectories(de)) CopiaDir(d, Path.Combine(para, Path.GetFileName(d)));
    }
    static void MigraPerfilAntigo()
    {
        string novo = Path.Combine(Program.Dados, "perfil-wv");
        if (Directory.Exists(novo)) return;
        string velho = Path.Combine(Program.Dados, "perfil", "Default");
        if (!Directory.Exists(velho)) return;
        string alvo = Path.Combine(novo, "EBWebView", "Default");
        foreach (string nome in new string[] { "Local Storage", "IndexedDB" })
        {
            string o = Path.Combine(velho, nome);
            if (Directory.Exists(o)) CopiaDir(o, Path.Combine(alvo, nome));
        }
        Log.Escreve("perfil antigo copiado para " + alvo);
    }

    string ScriptHost()
    {
        Dictionary<string, object> h = new Dictionary<string, object>();
        h["versao"] = "1"; h["fecharNaBandeja"] = cfg.FecharNaBandeja;
        h["iaUrl"] = null; h["iaPronta"] = false;
        return "window.__host = " + js.Serialize(h) + ";";
    }

    async Task IniciaWeb()
    {
        try
        {
            MigraPerfilAntigo();
            CoreWebView2EnvironmentOptions opts = new CoreWebView2EnvironmentOptions(); opts.Language = "pt-BR";
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Program.Dados, "perfil-wv"), opts);
            await web.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            Log.Escreve("WebView2: " + ex.Message);
            if (MessageBox.Show("O Orçamento precisa do componente Microsoft Edge WebView2 (já vem no Windows 11 e no Edge atualizado).\n\nDeseja abrir a página de download?\n\n" + ex.Message, "Orçamento", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                try { Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703"); } catch { }
            SairDeVez(); return;
        }
        CoreWebView2 c = web.CoreWebView2;
        c.Settings.AreDevToolsEnabled = Program.Teste;
        c.Settings.AreDefaultContextMenusEnabled = Program.Teste;
        c.Settings.IsStatusBarEnabled = false;
        c.Settings.IsPasswordAutosaveEnabled = false;
        c.Settings.IsGeneralAutofillEnabled = false;
        c.Settings.AreBrowserAcceleratorKeysEnabled = true;

        c.PermissionRequested += delegate (object s, CoreWebView2PermissionRequestedEventArgs e)
        {   // so a pagina local do app; so microfone (ditado por voz) e arquivos/pasta (backup automatico)
            bool local = e.Uri != null && e.Uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase);
            bool ok = local && (e.PermissionKind == CoreWebView2PermissionKind.Microphone || e.PermissionKind == CoreWebView2PermissionKind.FileReadWrite);
            e.State = ok ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
            Log.Escreve("permissao " + e.PermissionKind + " " + e.Uri + " -> " + e.State);
        };
        c.NavigationStarting += delegate (object s, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
            e.Cancel = true;
            if (e.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) try { Process.Start(e.Uri); } catch { }
        };
        c.NewWindowRequested += delegate (object s, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            if (e.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) try { Process.Start(e.Uri); } catch { }
        };
        c.WebMessageReceived += delegate (object s, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string txt = e.TryGetWebMessageAsString();
                Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(txt);
                object tipo; d.TryGetValue("tipo", out tipo);
                if ("config".Equals(tipo))
                {
                    object v; if (d.TryGetValue("fecharNaBandeja", out v) && v is bool) { cfg.FecharNaBandeja = (bool)v; cfg.Salvar(); }
                }
                else if ("sair".Equals(tipo)) BeginInvoke(new Action(SairDeVez));
            }
            catch (Exception ex) { Log.Escreve("mensagem invalida: " + ex.Message); }
        };
        await c.AddScriptToExecuteOnDocumentCreatedAsync(ScriptHost());
        c.Navigate(new Uri(Path.Combine(Program.AppDir, "index.html")).AbsoluteUri);
    }

    /* ---------- bandeja ---------- */
    async void Ocultar()
    {
        try
        {
            try { if (web.CoreWebView2 != null) await web.CoreWebView2.ExecuteScriptAsync("(function(){var b=document.getElementById('bloquear'); if(b) b.click();})()"); } catch (Exception ex) { Log.Escreve("bloquear: " + ex.Message); }
            await Task.Delay(120);   // deixa o app gravar e bloquear antes de esconder
            Hide();
            if (!cfg.AvisoBandeja)
            {
                tray.ShowBalloonTip(6000, "Orçamento", "O Orçamento continua aberto na bandeja. Clique no ícone para abrir.", ToolTipIcon.Info);
                cfg.AvisoBandeja = true; cfg.Salvar();
            }
        }
        catch (Exception ex) { Log.Escreve("ERRO ao ocultar: " + ex); }
    }
    void Restaurar()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = Program.Teste ? FormWindowState.Normal : FormWindowState.Maximized;
        Activate(); SetForegroundWindow(Handle);
    }
    async void SairDeVez()
    {
        if (sairDeVez) return;
        sairDeVez = true;
        try { if (web.CoreWebView2 != null) { await web.CoreWebView2.ExecuteScriptAsync("window.dispatchEvent(new Event('pagehide'))"); await Task.Delay(300); } } catch { }
        Close();
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Log.Escreve("fechando: motivo=" + e.CloseReason + " sairDeVez=" + sairDeVez + " bandeja=" + cfg.FecharNaBandeja);
        if (!sairDeVez && e.CloseReason == CloseReason.UserClosing && cfg.FecharNaBandeja)
        {
            e.Cancel = true; Ocultar(); return;
        }
        try { tray.Visible = false; tray.Dispose(); } catch { }
        base.OnFormClosing(e);
    }

    /* ---------- controle para auto-testes (--teste) ---------- */
    string Executa(string cmd)
    {
        try
        {
            if (cmd == "status")
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                Invoke(new Action(delegate
                {
                    d["visivel"] = Visible; d["minimizado"] = WindowState == FormWindowState.Minimized; d["hwnd"] = Handle.ToInt64();
                    d["bandeja"] = tray.Visible; d["fecharNaBandeja"] = cfg.FecharNaBandeja; d["avisoBandeja"] = cfg.AvisoBandeja;
                }));
                d["pid"] = Process.GetCurrentProcess().Id;
                return js.Serialize(d);
            }
            if (cmd == "abrir") { Invoke(new Action(Restaurar)); return "ok"; }
            if (cmd == "x") { Invoke(new Action(delegate { Close(); })); return "ok"; }
            if (cmd == "sair") { Invoke(new Action(SairDeVez)); return "ok"; }
            if (cmd.StartsWith("eval "))
            {   // avalia JS na pagina com "gesto do usuario" (Runtime.evaluate) e espera Promises
                for (int i = 0; i < 120; i++)
                {   // espera o WebView2 iniciar e a pagina do app carregar
                    bool pronto = (bool)Invoke(new Func<bool>(delegate { return web.CoreWebView2 != null && web.CoreWebView2.Source != null && web.CoreWebView2.Source.StartsWith("file:"); }));
                    if (pronto) break; Thread.Sleep(250);
                }
                Dictionary<string, object> p = new Dictionary<string, object>();
                p["expression"] = cmd.Substring(5); p["awaitPromise"] = true; p["returnByValue"] = true; p["userGesture"] = true;
                Task<string> t = (Task<string>)Invoke(new Func<Task<string>>(delegate { return web.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", js.Serialize(p)); }));
                if (!t.Wait(60000)) return "{\"erro\":\"tempo esgotado\"}";
                return t.Result;
            }
            return "{\"erro\":\"comando desconhecido\"}";
        }
        catch (Exception ex) { return "{\"erro\":\"" + ex.Message.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ") + "\"}"; }
    }
    void ServidorTeste()
    {
        string nome = "OrcamentoHost_" + Program.Hash;
        while (true)
        {
            try
            {
                using (NamedPipeServerStream ps = new NamedPipeServerStream(nome, PipeDirection.InOut, 1, PipeTransmissionMode.Byte))
                {
                    ps.WaitForConnection();
                    StreamReader rd = new StreamReader(ps, new UTF8Encoding(false)); StreamWriter wr = new StreamWriter(ps, new UTF8Encoding(false)); wr.AutoFlush = true;
                    string cmd = rd.ReadLine();
                    if (cmd != null) wr.WriteLine(Executa(cmd).Replace("\r", " ").Replace("\n", " "));
                }
            }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex) { Log.Escreve("pipe: " + ex.Message); Thread.Sleep(200); }
        }
    }
}
