Imports ElnBase
Imports Microsoft.Win32
Imports System.Windows.Threading

''' <summary>
''' UpdateCheck system standby compatible background process for periodically determining if a newer application 
''' version has become available.
''' </summary>
''' 
Public Class UpdateCheck

    Public Event UpdateAvailable(sender As Object, newVersion As String)

    Private WithEvents _UpdateCheckTimer As New DispatcherTimer
    Private _currAppVersion As Version

    ''' <summary>
    ''' Initializes the update checker.
    ''' </summary>
    ''' <param name="currAppVersion">The current application version.</param>
    ''' <param name="minCheckInterval">The time interval for online version checks.</param>
    ''' the minCheckInterval.</param>
    '''
    Public Sub New(currAppVersion As Version, minCheckInterval As TimeSpan)

        _currAppVersion = currAppVersion

        With _UpdateCheckTimer
            .Interval = minCheckInterval
            .Start()
        End With

        ' immediately perform async check
        CheckForUpdatesAsync()

    End Sub


    ''' <summary>
    ''' Important: Even after waking from standby, DispatcherTimer will issue ticks correctly!
    ''' </summary>
    ''' 
    Private Sub UpdateCheckTimer_Tick() Handles _UpdateCheckTimer.Tick

        CheckForUpdatesAsync()

    End Sub


    ''' <summary>
    ''' Retrieves the latest application version online and updates the update notification panel in 
    ''' the application sidebar accordingly.</summary>
    ''' <remarks>It may take a few minutes until updates to the online version database 
    ''' actually become available to the PHP service.</remarks>
    ''' 
    Private Async Sub CheckForUpdatesAsync()

        ' get online latest version
        Await Task.Delay(8000) ' wait to ensure that the network connection is re-established if waking up from stand-by
        Dim newVersionStr = Await PhpServices.GetLatestAppVersionAsync
        If newVersionStr = "" Then
            Exit Sub 'server error
        End If

        ' notify if new version available
        Dim latestVersion = New Version(newVersionStr)
        If _currAppVersion < latestVersion Then
            RaiseEvent UpdateAvailable(Me, newVersionStr)
        End If

    End Sub

End Class
