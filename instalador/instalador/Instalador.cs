// Instalar Orcamento.exe - instalador e desinstalador simples (sem administrador). C# 5 (csc do Windows).
//  * O pacote do app vai embutido como recurso "payload.zip" (montado por montar-pacote.ps1).
//  * Argumentos para automacao/testes:
//      /silencioso  /dir <pasta>  /sematalhos  /semabrir  /desktop <pasta>  /menu <pasta>  /chave <nome>  /log <arquivo>
//      /desinstalar  [/dados <pasta>]  [/apagardados sim|nao]
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Cfg
{
    public const string Versao = "2.0";
    public const string NomeAtalho = "Orçamento comprometido";
}

class Opcoes
{
    public string Dir, Dados, DesktopDir, MenuDir, Chave = "Orcamento", Log, ApagarDados;
    public bool AtalhoDesktop = true, AtalhoMenu = true, Silencioso, SemAbrir, Desinstalar;
}

static class Utils
{
    public static string DadosPadrao() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orcamento"); }
    public static string DirPadrao() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Orcamento"); }

    public static void Atalho(string arq, string alvo, string trab, string icone)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell"); object sh = Activator.CreateInstance(t);
        object l = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { arq });
        Type lt = l.GetType();
        lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, l, new object[] { alvo });
        lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, l, new object[] { trab });
        lt.InvokeMember("Description", BindingFlags.SetProperty, null, l, new object[] { Cfg.NomeAtalho });
        if (icone != null && File.Exists(icone)) lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, l, new object[] { icone });
        lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, l, null);
    }

    // encerra o Orcamento.exe que estiver rodando DESTA pasta (nunca por nome sozinho)
    public static void FechaOrcamento(string pasta)
    {
        foreach (Process p in Process.GetProcessesByName("Orcamento"))
        {
            try { if (p.MainModule.FileName.StartsWith(pasta, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(4000); } } catch { }
        }
    }
}

class Instalacao
{
    public Opcoes O; public Action<int, string> Prog; public Action<string> Log; public volatile bool Cancelar;
    public Instalacao(Opcoes o, Action<int, string> prog, Action<string> log) { O = o; Prog = prog; Log = log; }

    public void InstalaApp()
    {
        Prog(-1, "Copiando o aplicativo…"); Log("Instalando em " + O.Dir);
        Directory.CreateDirectory(O.Dir);
        Utils.FechaOrcamento(O.Dir);
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
        {
            if (s == null) throw new Exception("Este instalador está incompleto (sem o pacote do aplicativo).");
            using (ZipArchive z = new ZipArchive(s, ZipArchiveMode.Read))
            {
                int i = 0;
                foreach (ZipArchiveEntry e in z.Entries)
                {
                    i++; if (Cancelar) throw new OperationCanceledException();
                    string dest = Path.Combine(O.Dir, e.FullName.Replace('/', '\\'));
                    if (e.Name == "") { Directory.CreateDirectory(dest); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    e.ExtractToFile(dest, true);
                    Prog((int)(100.0 * i / z.Entries.Count), "Copiando o aplicativo…");
                }
            }
        }
        try
        {
            string eu = Assembly.GetExecutingAssembly().Location, dst = Path.Combine(O.Dir, "Desinstalar.exe");
            if (!string.Equals(Path.GetFullPath(eu), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase)) File.Copy(eu, dst, true);
        }
        catch (Exception ex) { Log("Aviso: não consegui copiar o desinstalador: " + ex.Message); }

        string exe = Path.Combine(O.Dir, "Orcamento.exe"), ico = Path.Combine(O.Dir, "icons", "app.ico");
        try
        {
            if (O.AtalhoDesktop)
            {
                string d = O.DesktopDir ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); Directory.CreateDirectory(d);
                Utils.Atalho(Path.Combine(d, Cfg.NomeAtalho + ".lnk"), exe, O.Dir, ico); Log("Atalho na Área de Trabalho criado.");
            }
            if (O.AtalhoMenu)
            {
                string d = O.MenuDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Programs); Directory.CreateDirectory(d);
                Utils.Atalho(Path.Combine(d, Cfg.NomeAtalho + ".lnk"), exe, O.Dir, ico); Log("Atalho no Menu Iniciar criado.");
            }
        }
        catch (Exception ex) { Log("Aviso: atalhos: " + ex.Message); }
        try
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + O.Chave))
            {
                k.SetValue("DisplayName", Cfg.NomeAtalho); k.SetValue("DisplayVersion", Cfg.Versao); k.SetValue("Publisher", "Orçamento");
                k.SetValue("InstallLocation", O.Dir); k.SetValue("DisplayIcon", ico);
                k.SetValue("UninstallString", "\"" + Path.Combine(O.Dir, "Desinstalar.exe") + "\" /desinstalar"
                    + (O.Chave != "Orcamento" ? " /chave " + O.Chave : "") + (O.DesktopDir != null ? " /desktop \"" + O.DesktopDir + "\"" : "") + (O.MenuDir != null ? " /menu \"" + O.MenuDir + "\"" : ""));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord); k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", 20000, RegistryValueKind.DWord);
            }
            Log("Registrado em Aplicativos instalados.");
        }
        catch (Exception ex) { Log("Aviso: registro de desinstalação: " + ex.Message); }
    }
}

static class Desinstala
{
    public static int Roda(Opcoes o)
    {
        string dir = o.Dir ?? AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        string dados = o.Dados ?? Utils.DadosPadrao();
        bool apagarDados;
        if (o.Silencioso) apagarDados = "sim".Equals(o.ApagarDados, StringComparison.OrdinalIgnoreCase);
        else
        {
            if (MessageBox.Show("Remover o Orçamento deste computador?", "Desinstalar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
            apagarDados = MessageBox.Show("Apagar também os SEUS DADOS (finanças criptografadas e configurações)?\n\nEscolha Não para mantê-los: se instalar de novo, tudo volta.", "Desinstalar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
        Utils.FechaOrcamento(dir); Thread.Sleep(500);
        string desk = o.DesktopDir ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), menu = o.MenuDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        foreach (string d in new string[] { desk, menu }) try { string f = Path.Combine(d, Cfg.NomeAtalho + ".lnk"); if (File.Exists(f)) File.Delete(f); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + o.Chave, false); } catch { }
        if (apagarDados) { try { Directory.Delete(dados, true); } catch { } }
        string eu = Assembly.GetExecutingAssembly().Location;
        foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) { if (string.Equals(f, eu, StringComparison.OrdinalIgnoreCase)) continue; try { File.Delete(f); } catch { } }
        foreach (string d in Directory.GetDirectories(dir)) try { Directory.Delete(d, true); } catch { }
        try   // este .exe so pode sumir depois que terminar
        {
            ProcessStartInfo p = new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + eu + "\" & rmdir \"" + dir + "\"");
            p.CreateNoWindow = true; p.UseShellExecute = false; Process.Start(p);
        }
        catch { }
        if (!o.Silencioso) MessageBox.Show("O Orçamento foi removido.", "Desinstalar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }
}

class TelaInstalador : Form
{
    Opcoes o; Panel p0, p1; Button btnAvanca, btnCancela;
    TextBox txtDir, log; CheckBox cbDesk, cbMenu, cbAbrir; ProgressBar barra; Label lblStatus;
    Thread th; Instalacao inst; bool concluido, emAndamento;

    public TelaInstalador(Opcoes op)
    {
        o = op;
        Text = "Instalar Orçamento comprometido"; ClientSize = new Size(660, 440); StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; Font = new Font("Segoe UI", 9.5f);
        try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }
        p0 = new Panel(); p1 = new Panel();
        foreach (Panel p in new Panel[] { p0, p1 }) { p.SetBounds(0, 0, 660, 390); p.Visible = false; Controls.Add(p); }
        btnAvanca = Botao("Instalar", 440); btnCancela = Botao("Cancelar", 550);
        btnAvanca.Click += delegate { Instala(); };
        btnCancela.Click += delegate { if (emAndamento) { if (inst != null) inst.Cancelar = true; } else Close(); };

        p0.Controls.Add(Rotulo("Bem-vindo ao Orçamento comprometido", 24, 22, 600, 30, new Font("Segoe UI Semibold", 15f)));
        p0.Controls.Add(Rotulo("Controle financeiro pessoal que funciona no seu computador, com seus dados criptografados e protegidos por senha.\r\nNão precisa de administrador.", 24, 64, 610, 50, null));
        p0.Controls.Add(Rotulo("Instalar em:", 24, 134, 300, 20, null));
        txtDir = new TextBox(); txtDir.SetBounds(24, 158, 500, 26); txtDir.Text = o.Dir; p0.Controls.Add(txtDir);
        Button proc = new Button(); proc.Text = "Procurar…"; proc.SetBounds(534, 156, 100, 28);
        proc.Click += delegate { using (FolderBrowserDialog f = new FolderBrowserDialog()) { f.SelectedPath = txtDir.Text; if (f.ShowDialog() == DialogResult.OK) txtDir.Text = Path.Combine(f.SelectedPath, "Orcamento"); } };
        p0.Controls.Add(proc);
        cbDesk = new CheckBox(); cbDesk.Text = "Criar atalho na Área de Trabalho"; cbDesk.Checked = true; cbDesk.SetBounds(24, 206, 400, 24); p0.Controls.Add(cbDesk);
        cbMenu = new CheckBox(); cbMenu.Text = "Criar atalho no Menu Iniciar"; cbMenu.Checked = true; cbMenu.SetBounds(24, 234, 400, 24); p0.Controls.Add(cbMenu);
        p0.Controls.Add(Rotulo("Seus dados ficam em " + o.Dados + " (separados do programa: atualizar ou reinstalar não apaga nada).\r\nAo clicar no X, o Orçamento vai para a bandeja (perto do relógio) e bloqueia; use o ícone da bandeja > \"Fechar de vez\" para sair.", 24, 282, 610, 70, null));

        p1.Controls.Add(Rotulo("Instalando…", 24, 16, 600, 28, new Font("Segoe UI Semibold", 14f)));
        lblStatus = Rotulo("", 24, 56, 610, 22, null); p1.Controls.Add(lblStatus);
        barra = new ProgressBar(); barra.SetBounds(24, 82, 610, 22); p1.Controls.Add(barra);
        log = new TextBox(); log.SetBounds(24, 116, 610, 220); log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.BackColor = Color.White; p1.Controls.Add(log);
        cbAbrir = new CheckBox(); cbAbrir.Text = "Abrir o Orçamento agora"; cbAbrir.Checked = true; cbAbrir.Visible = false; cbAbrir.SetBounds(24, 348, 300, 24); p1.Controls.Add(cbAbrir);
        p0.Visible = true;
    }
    Button Botao(string t, int x) { Button b = new Button(); b.Text = t; b.SetBounds(x, 396, 100, 30); Controls.Add(b); return b; }
    Label Rotulo(string t, int x, int y, int w, int h, Font f) { Label l = new Label(); l.Text = t; l.SetBounds(x, y, w, h); if (f != null) l.Font = f; return l; }
    void Escreve(string m) { if (!IsDisposed) BeginInvoke(new Action(delegate { log.AppendText(m + "\r\n"); })); }
    void Progresso(int pc, string t)
    {
        if (IsDisposed) return;
        BeginInvoke(new Action(delegate { lblStatus.Text = t; if (pc < 0) barra.Style = ProgressBarStyle.Marquee; else { barra.Style = ProgressBarStyle.Continuous; barra.Value = Math.Min(100, pc); } }));
    }
    void Instala()
    {
        if (txtDir.Text.Trim() == "") { MessageBox.Show("Escolha a pasta de instalação."); return; }
        o.Dir = txtDir.Text.Trim(); o.AtalhoDesktop = cbDesk.Checked; o.AtalhoMenu = cbMenu.Checked;
        p0.Visible = false; p1.Visible = true; btnAvanca.Visible = false; emAndamento = true;
        inst = new Instalacao(o, Progresso, Escreve);
        th = new Thread(delegate ()
        {
            bool ok = true;
            try { inst.InstalaApp(); }
            catch (OperationCanceledException) { Escreve("Instalação cancelada."); ok = false; }
            catch (Exception ex) { Escreve("ERRO: " + ex.Message); ok = false; }
            BeginInvoke(new Action(delegate { Fim(ok); }));
        });
        th.IsBackground = true; th.Start();
    }
    void Fim(bool ok)
    {
        emAndamento = false; barra.Style = ProgressBarStyle.Continuous; barra.Value = ok ? 100 : 0;
        if (!ok) { lblStatus.Text = "A instalação não foi concluída."; btnCancela.Text = "Fechar"; return; }
        concluido = true; lblStatus.Text = "Pronto! O Orçamento foi instalado."; cbAbrir.Visible = true; btnCancela.Text = "Concluir";
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (concluido && cbAbrir.Checked)
            try { Process.Start(new ProcessStartInfo(Path.Combine(o.Dir, "Orcamento.exe")) { UseShellExecute = false, WorkingDirectory = o.Dir }); } catch { }
    }
}

static class Program
{
    static string Arg(string[] a, string n) { for (int i = 0; i < a.Length - 1; i++) if (string.Equals(a[i], n, StringComparison.OrdinalIgnoreCase)) return a[i + 1]; return null; }
    static bool Tem(string[] a, string n) { return Array.Exists(a, delegate (string x) { return string.Equals(x, n, StringComparison.OrdinalIgnoreCase); }); }

    [STAThread]
    static int Main(string[] args)
    {
        Opcoes o = new Opcoes();
        o.Dir = Arg(args, "/dir"); o.Dados = Arg(args, "/dados") ?? Utils.DadosPadrao();
        o.DesktopDir = Arg(args, "/desktop"); o.MenuDir = Arg(args, "/menu"); o.Chave = Arg(args, "/chave") ?? "Orcamento"; o.Log = Arg(args, "/log");
        o.Silencioso = Tem(args, "/silencioso"); o.SemAbrir = Tem(args, "/semabrir"); o.Desinstalar = Tem(args, "/desinstalar"); o.ApagarDados = Arg(args, "/apagardados");
        if (Tem(args, "/sematalhos")) { o.AtalhoDesktop = false; o.AtalhoMenu = false; }
        if (o.Desinstalar) { Application.EnableVisualStyles(); return Desinstala.Roda(o); }
        if (o.Dir == null) o.Dir = Utils.DirPadrao();
        if (o.Silencioso)
        {
            Action<string> log = delegate (string m) { if (o.Log != null) try { File.AppendAllText(o.Log, m + "\r\n", Encoding.UTF8); } catch { } };
            Instalacao i = new Instalacao(o, delegate (int p, string t) { }, log);
            try { i.InstalaApp(); } catch (Exception ex) { log("ERRO: " + ex.Message); return 1; }
            log("CONCLUIDO"); return 0;
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TelaInstalador(o));
        return 0;
    }
}
