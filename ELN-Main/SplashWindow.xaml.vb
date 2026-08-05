''' <summary>
''' Small, non-intrusive placeholder shown between process start and MainWindow's first real
''' render, so the user gets immediate feedback that the app launched instead of a silent pause.
''' </summary>
'''
Class SplashWindow

    Public Sub New()

        InitializeComponent()

        txtVersion.Text = "V." & Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3)

        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2
        Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - Height) / 2

    End Sub

End Class
