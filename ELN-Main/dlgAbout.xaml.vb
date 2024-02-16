Public Class dlgAbout

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    Private Sub lnkChemBytes_Click() Handles lnkChemBytes.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://chembytes.com/phoenix-eln/about/")
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub lnkGitHub_Click() Handles lnkGitHub.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://github.com/abrechts/Phoenix-ELN/")
        info.UseShellExecute = True
        Process.Start(info)

    End Sub

End Class
