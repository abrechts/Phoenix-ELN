Public Class MainStatusBar

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets if the server status items should be displayed or not.
    ''' </summary>
    ''' 
    Public WriteOnly Property DisplayServerStatus() As Boolean

        Set(value As Boolean)
            If value = True Then
                pnlServerState.Visibility = Windows.Visibility.Visible
            Else
                pnlServerState.Visibility = Windows.Visibility.Collapsed
            End If
        End Set

    End Property


    ''' <summary>
    ''' Sets if a server error should be displayed or not
    ''' </summary>
    ''' 
    Public WriteOnly Property DisplayServerError() As Boolean

        Set(value As Boolean)
            If value = True Then
                pnlServerError.Visibility = Windows.Visibility.Visible
                pnlServerOk.Visibility = Windows.Visibility.Collapsed
                If My.Settings.IsServerOffByUser Then
                    pnlServerError.ToolTip = "ELN server disconnected by user."
                Else
                    pnlServerError.ToolTip = "The ELN server currently is unavailable!"
                End If
            Else
                pnlServerError.Visibility = Windows.Visibility.Collapsed
                pnlServerOk.Visibility = Windows.Visibility.Visible
            End If
        End Set

    End Property


    Public Sub AnimateSaveIcon()

        'animate save label
        Dim anim As New Animations
        anim.FadeInAndOut(lblSaving, peakOpacity:=0.8, fadeMillisec:=300, holdMillisec:=200)

    End Sub


End Class
