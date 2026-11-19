using System.Windows;

namespace MobaSkillBar;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        using var currentIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var instance = new Mutex(false, $"Local\\MobaSkillBar-{currentIdentity.User?.Value}");
        try
        {
            if (!instance.WaitOne(0))
            {
                instance.Dispose();
                MessageBox.Show("技能栏已在运行。请从任务栏打开控制窗口。", "MOBA 技能栏");
                Shutdown();
                return;
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous process exited before releasing the mutex.
        }

        Exit += (_, _) =>
        {
            instance.ReleaseMutex();
            instance.Dispose();
        };
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
