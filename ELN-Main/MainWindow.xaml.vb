Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Text.RegularExpressions
Imports System.Windows.Interop
Imports System.Windows.Threading
Imports CustomControls
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Win32

Class MainWindow

    ''' <summary>
    ''' Blocks only the native title-bar drag gesture (not general window input) until startup-critical
    ''' work has finished.
    ''' </summary>
    ''' 
    Private _blockCaptionDrag As Boolean = True

    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const WM_NCLBUTTONDBLCLK As Integer = &HA3
    Private Const WM_SYSCOMMAND As Integer = &H112
    Private Const HTCAPTION As Integer = 2
    Private Const SC_MOVE_MASK As Integer = &HFFF0
    Private Const SC_MOVE As Integer = &HF010

    Private Sub Me_SourceInitialized() Handles Me.SourceInitialized

        Dim hwndSource = TryCast(PresentationSource.FromVisual(Me), HwndSource)
        hwndSource?.AddHook(AddressOf BlockCaptionDragHook)

        'safety net: never leave the window permanently un-draggable if some startup path fails to
        'clear the block (e.g. an unanticipated exception, or a slow/stuck server round-trip)
        Dim fallbackTimer As New DispatcherTimer With {.Interval = TimeSpan.FromSeconds(5)}
        AddHandler fallbackTimer.Tick, Sub(sender As Object, e As EventArgs)
                                           AllowCaptionDrag()
                                           fallbackTimer.Stop()
                                       End Sub
        fallbackTimer.Start()

    End Sub

    Private Function BlockCaptionDragHook(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr

        If _blockCaptionDrag Then
            Select Case msg
                Case WM_NCLBUTTONDOWN, WM_NCLBUTTONDBLCLK
                    If (wParam.ToInt32() And &HFFFF) = HTCAPTION Then
                        handled = True
                        Return IntPtr.Zero
                    End If
                Case WM_SYSCOMMAND
                    If (wParam.ToInt32() And SC_MOVE_MASK) = SC_MOVE Then
                        handled = True
                        Return IntPtr.Zero
                    End If
            End Select
        End If

        Return IntPtr.Zero

    End Function

    ''' <summary>
    ''' Re-allows the native title-bar drag gesture once startup-critical work has finished. Safe to call
    ''' repeatedly/redundantly.
    ''' </summary>
    '''
    Private Sub AllowCaptionDrag()

        _blockCaptionDrag = False

    End Sub

    Friend Shared Property DBContext As ElnDbContext

    Friend Shared Property ServerDBContext As ElnDbContext

    Friend Shared Property SQLiteDbPath As String

    Friend Shared Property ApplicationVersion As Version

    Private Property ExpDisplayList As ObservableCollection(Of tblExperiments)

    ''' <summary>
    ''' Stashes the original DisplayIndex of an experiment belonging to another local user,
    ''' while it is temporarily previewed (DisplayIndex = -3) in the current user's tab bar.
    ''' </summary>
    Private ReadOnly ForeignExpOriginalDisplayIndex As New Dictionary(Of tblExperiments, Short?)

    Private WithEvents UpdateChecker As UpdateCheck

    Private _IsVersionUpgrade As Boolean = False

    Private _MigrationType As Integer = MigrationType.None

    Public Sub New()

        'Apply the user's dark/light skin before InitializeComponent() builds the visual tree, so
        'DynamicResource-bound brushes resolve to the correct skin on the very first paint, instead of
        'briefly rendering the default skin until Me_Loaded's own ApplySkin call (kept there as a safety
        'net for the rare case where a settings version upgrade changes the persisted value) catches up.

        CustomControls.DarkModeHelper.ApplySkin(CustomControls.My.MySettings.Default.IsDarkMode)

        InitializeComponent()

        'Prevent another instance of the application to be run
        If (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1) Then
            Me.Hide()
            cbMsgBox.Display("Phoenix ELN is already running! ", MsgBoxStyle.Information, "Phoenix ELN")
            Application.Current.Shutdown()
        End If

    End Sub


    Private Async Sub Me_Loaded() Handles Me.Loaded

        Dim isRestoreFromServer As Boolean = False

        'important for displaying the application icon in the taskbar!
        Await Task.Yield

        Dim dbFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\Phoenix ELN Data"
        SQLiteDbPath = dbFolderPath + "\ElnData.db"

        ' Create local data folder from code, if not created by installer for current user (e.g. after admin installation)
        If Not Directory.Exists(dbFolderPath) Then
            Directory.CreateDirectory(dbFolderPath)
        End If

        ' Replace local database by demo database, if missing for some reason
        If Not File.Exists(SQLiteDbPath) Then
            File.Copy("ElnData_Seed.db", SQLiteDbPath)
            DbUpgradeLocal.Upgrade(SQLiteDbPath)
        End If

        With CustomControls.My.MySettings.Default

            'Upgrade settings
            If .SettingIsNew Then
                .Upgrade()
                .SettingIsNew = False
                .Save()
                _IsVersionUpgrade = True
            End If

            Me.IsDarkMode = .IsDarkMode
            CustomControls.DarkModeHelper.ApplySkin(.IsDarkMode) 'apply skin explicitly to be on safe side

            '------------------------
            '_IsVersionUpgrade = True
            '------------------------

            'Check for pending transfer pack data migration
            If .DataMigrationType <> MigrationType.None Then
                Threading.Thread.Sleep(50)                  'allow previous instance to close
                _MigrationType = .DataMigrationType         'for use in Me_ContentRendered
                TransferPackage.PerformPendingMigration()   'overwrites current settings
                .DataMigrationType = MigrationType.None
                .RestoreFromServer = False
                .Save()
                DbUpgradeLocal.Upgrade(SQLiteDbPath)        'since the transfer package db could contain an earlier version db
            End If

            'Apply application window settings
            If .StartupSize.Width > -1 Then
                WindowStartupLocation = WindowStartupLocation.Manual
                Left = .StartupPosition.X
                Top = .StartupPosition.Y
                Width = .StartupSize.Width
                Height = .StartupSize.Height
            Else
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            End If

            'Check for pending restore
            If .RestoreFromServer = True Then

                Threading.Thread.Sleep(50)  'allow previous instance to close
                Dim tempPath = IO.Path.GetDirectoryName(SQLiteDbPath) + "\ElnData.tmp"
                Try
                    File.Move(tempPath, SQLiteDbPath, True)    'overwrite current working db
                Catch ex As Exception
                    cbMsgBox.Display("Can't replace current experiments database for restore, since it is currently " +
                        "locked by another application!", MsgBoxStyle.Information, "Phoenix ELN Restore Error")
                End Try
                isRestoreFromServer = True
                .RestoreFromServer = False
                .Save()

            End If

            'Create the spell checker (required by UI controls that depend on it during initialization)
            SpellChecker.Initialize(.LastSpellCheckLocale, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) & "\Phoenix ELN Data\customWords.lex")

        End With

        'Apply database upgrades for new version if required
        If _IsVersionUpgrade Then
            DbUpgradeLocal.Upgrade(SQLiteDbPath)
            DbUpgradeServer.IsUpgradeCheckRequired = True
        End If

        'Create local SqliteContext
        DBContext = New SQLiteContext(SQLiteDbPath).ElnContext

        'Open the dedicated in-memory full-text SearchIndex connection (see FullTextSearch for details) - must
        'happen before anything below touches search, and before any SaveChanges call needs to apply an
        'incremental SearchIndex change.
        FullTextSearch.InitializeMemoryIndexConnection(SQLiteDbPath)

        'Check for legacy Rss backlog registration (--> can be removed later!)
        If New Version(DBContext.tblDatabaseInfo.First.CurrAppVersion) < New Version("0.9.5") Then
            Dim rss As New RxnSubstructure
            rss.RegisterRssBacklog(DBContext.tblDatabaseInfo.First.tblUsers.First)
            DBContext.SaveChanges()
        End If

        'Check for backlog registration of experiment IsReactRacemicReactant and IsRacemicProduct (can be removed later)
        If New Version(DBContext.tblDatabaseInfo.First.CurrAppVersion) < New Version("4.1.0") Then
            SketchArea.RegisterIsRacemicBacklog(DBContext)
        End If

        'Version upgrade to project folders (from v.3.0.0 on) - one-time initialize default folders
        If _IsVersionUpgrade OrElse isRestoreFromServer Then
            If Not DBContext.tblProjFolders.Any Then
                ProjectFolders.Initialize(DBContext)
            End If
        End If

        If isRestoreFromServer Then
            '  Restored legacy DB's may be lacking tblProjFolders (from v.3.0.0 on) initializations.
            ProjectFolders.SetMissingProjFolderRefs(DBContext)
            '  Reset all sync flags
            DBContext.ResetSyncFlags()
        End If

        'Backfill the persisted tblSearchIndex table if it's empty, e.g. right after it was first created by
        'DbUpgradeLocal, or after running with the seed database, or a restore that brought in an older/emptied database. Must run AFTER the
        'isRestoreFromServer block above, not before to preserve the sync flags for tblSearchIndex.

        If FullTextSearch.IsSearchIndexEmpty(DBContext) Then
            FullTextSearch.RebuildSearchIndex(DBContext)
        End If

        'Fill the in-memory FTS5 SearchIndex from tblSearchIndex - always, since the in-memory table starts
        'out empty every session (a fast, single set-based copy; see FullTextSearch.HydrateMemoryIndex).

        FullTextSearch.HydrateMemoryIndex()

        ApplicationVersion = GetType(MainWindow).Assembly.GetName().Version
        DBContext.tblDatabaseInfo.First.CurrAppVersion = ApplicationVersion.ToString

        'Version update: Send install statistics
        If _IsVersionUpgrade Then
            PhpServices.SendInstallInfo(ApplicationVersion.ToString(3), DBContext, CustomControls.My.MySettings.Default.IsServerSpecified)
        End If

        RemainingDemoCountConverter.MaxDemoCount = 15
        RemainingDemoCountConverter.FactoryDemoCount = 6

        'register shared events
        AddHandler Protocol.RequestSaveIcon, AddressOf Protocol_RequestSaveIcon
        AddHandler UndoRedo.CanUndoChanged, AddressOf UndoRedo_CanUndoChanged
        AddHandler UndoRedo.CanRedoChanged, AddressOf UndoRedo_CanRedoChanged
        AddHandler StatusDemo.RequestCreateFirstUser, AddressOf DemoStatus_RequestCreateFirstUser
        AddHandler StatusDemo.RequestRestoreServer, AddressOf DemoStatus_RequestRestoreServer
        AddHandler StatusDemo.RequestTransferPackage, AddressOf DemoStatus_RequestTransferPackage
        AddHandler ServerSync.ServerContextCreated, AddressOf ServerSync_ServerContextCreated
        AddHandler Protocol.ConnectedChanged, AddressOf Protocol_ConnectedChanged
        AddHandler ServerSync.SyncProgress, AddressOf ServerSync_SyncProgress
        AddHandler dlgServerConnection.ServerContextCreated, AddressOf ServerSync_ServerContextCreated
        AddHandler ExpTabHeader.PinStateChanged, AddressOf expTabHeader_PinStateChanged
        AddHandler StepSummary.RequestOpenExperiment, AddressOf ResultList_RequestOpenExperiment
        AddHandler RssItemGroup.RequestOpenExperiment, AddressOf RssItemGroup_RequestOpenExperiment
        AddHandler StepExpSelector.RequestOpenExperiment, AddressOf StepExpSelector_RequestOpenExperiment
        AddHandler dlgFullTextSearch.RequestOpenExperiment, AddressOf FullTextSearch_RequestOpenExperiment
        AddHandler RssItemGroup.RequestStepConnections, AddressOf ExperimentContent_RequestSequencesDialog
        AddHandler ExperimentContent.RequestConnectionGraph, AddressOf ExperimentContent_RequestSequencesDialog
        AddHandler ExperimentContent.RequestStructureGraph, AddressOf ExperimentContent_RequestStructureGraph
        AddHandler ExperimentContent.ExperimentContextChanged, AddressOf ExperimentContent_ContextChanged


        'determine current localUser (since multiple users possible)
        Dim currUser = (From user In DBContext.tblUsers Where user.IsCurrent = 1).FirstOrDefault
        If currUser Is Nothing Then
            currUser = DBContext.tblUsers.First
            currUser.IsCurrent = 1
        End If

        'connect local database model with UI
        ApplyAllDataBindings(currUser)
        expNavTree.cboUsers.SelectedItem = currUser

        'update spell checker header with last used locale
        For Each spellItem In mnuSpelling.Items(0).items
            If TypeOf spellItem Is MenuItem Then
                If spellItem.Tag = CustomControls.My.MySettings.Default.LastSpellCheckLocale Then
                    txtSpellHeader.Text = spellItem.Header
                    txtSpellHeader.ToolTip = spellItem.ToolTip
                    Exit For
                End If
            End If
        Next

        'create server context async to reduce startup time
        With CustomControls.My.MySettings.Default

            If DBContext.tblDatabaseInfo.First.tblUsers.First.UserID <> "demo" Then

                If .IsServerSpecified Then
                    If Not .IsServerOffByUser Then

                        If .IsServerQuery Then
                            'disable search until server conn established
                            mnuSearch.IsEnabled = False
                        End If

                        ServerSync.CreateServerContextAsync(.ServerName, .ServerDbUserName, .ServerDbPassword, .ServerPort,
                           DBContext.tblDatabaseInfo.First) 'handled by ServerSync_ServerContextCreated (also sets mainStatusInfo, calls AllowCaptionDrag())

                    Else
                        'manually disconnected by localUser
                        mainStatusInfo.DisplayServerError = True
                        AllowCaptionDrag()
                    End If

                Else
                    AllowCaptionDrag()
                End If
                btnAddUser.IsEnabled = True
            Else
                btnAddUser.IsEnabled = False
                'demo localUser has no server connection
                .IsServerSpecified = False 'visibility of server status items is data bound to this setting
                AllowCaptionDrag()
            End If

        End With

        'Defer expensive layout and scroll operations to after the first render pass completes.
        'This allows the UI to become visible immediately instead of blocking on UpdateLayout().
        Dim currExp = (From exp In currUser.tblExperiments Where exp.IsCurrent).FirstOrDefault
        If currExp IsNot Nothing Then
            Dispatcher.BeginInvoke(Sub()
                                       Try
                                           Me.UpdateLayout()
                                           expNavTree.ScrollExperimentIntoView(currExp)
                                           Dim thisTab As TabItem = tabExperiments.ItemContainerGenerator.ContainerFromItem(currExp)
                                           If thisTab IsNot Nothing Then
                                               thisTab.IsSelected = True
                                           End If
                                       Catch ex As Exception
                                           'in case of any error, e.g. due to missing content, just ignore and continue with unselected tab
                                           cbMsgBox.Display("Your last used experiment seems to have a technical issue. Please check its content.",
                      MsgBoxStyle.Exclamation, "Startup Issue")
                                       End Try
                                   End Sub, System.Windows.Threading.DispatcherPriority.ApplicationIdle)
        End If

        'start the periodic cleanup process for embedded document editing resources (currently set to every hour)
        FileContent.StartDocEditCleanupTimer(TimeSpan.FromHours(1))

        'Regularly check for version updates (currently every 12 h)
        UpdateChecker = New UpdateCheck(ApplicationVersion, TimeSpan.FromHours(12))

    End Sub


    ''' <summary>
    ''' Main window content rendered: Display startup messages here.
    ''' </summary>
    '''
    Private Sub Me_ContentRendered() Handles Me.ContentRendered

        pnlStartupOverlay.Visibility = Visibility.Collapsed

        ' determine if a data migration was performed

        Select Case _MigrationType

            Case MigrationType.ReplaceProductive

                cbMsgBox.Display("Your ELN data migration is complete now! " + vbCrLf + vbCrLf +
                    "The transfer package 'recovery.elnpkg' was placed " +
                    "on your desktop, which you can import for reverting " +
                    "this migration in case of any issues." + vbCrLf + vbCrLf +
                    "It is recommended to delete this file after successful migration.",
                    MsgBoxStyle.Information, "Data Migration Complete")
                _MigrationType = MigrationType.None

            Case MigrationType.ReplaceDemo

                cbMsgBox.Display("Your ELN data migration is complete now! ", MsgBoxStyle.Information, "Data Migration")
                _MigrationType = MigrationType.None

            Case MigrationType.Recovery

                cbMsgBox.Display("Your ELN data migration was successfully reverted! " + vbCrLf + vbCrLf +
                    "It is recommended to now delete the recovery file 'recovery.elnpkg', located on your desktop.", MsgBoxStyle.Information, "Data Migration")
                _MigrationType = MigrationType.None

        End Select

    End Sub


    ''' <summary>
    ''' Sets or gets if the main window is to be rendered in dark mode display
    ''' </summary>
    ''' 
    Public Shared ReadOnly IsDarkModeProperty As DependencyProperty = DependencyProperty.Register(NameOf(IsDarkMode), GetType(Boolean), GetType(MainWindow),
            New PropertyMetadata(True, AddressOf OnIsDarkModeChanged))

    Public Property IsDarkMode As Boolean
        Get
            Return DirectCast(GetValue(IsDarkModeProperty), Boolean)
        End Get
        Set(value As Boolean)
            SetValue(IsDarkModeProperty, value)
        End Set
    End Property

    Private Shared Sub OnIsDarkModeChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        CustomControls.DarkModeHelper.ApplySkin(CBool(e.NewValue))
        CustomControls.My.MySettings.Default.IsDarkMode = CBool(e.NewValue)
        CustomControls.My.MySettings.Default.Save()
    End Sub



    ''' <summary>
    ''' Handles version update availability message
    ''' /summary>
    '''
    Private Sub UpdateChecker_UpdateAvailable(sender As Object, newVersion As String) Handles UpdateChecker.UpdateAvailable

        pnlStatus.ShowAvailableUpdate(newVersion)

    End Sub


    ''' <summary>
    ''' Shared handler for actions after completed server connection.
    ''' </summary>
    ''' 
    Private Async Sub ServerSync_ServerContextCreated(serverContext As ElnDbContext)

        Try

            Me.Cursor = Cursors.Arrow
            Me.ForceCursor = False

            mnuSearch.IsEnabled = True

            If serverContext IsNot Nothing Then

                '- handle syncID mismatch

                ServerDBContext = serverContext

                If ServerSync.HasSyncMismatch Then

                    Dim syncMismatchWarningDlg As New dlgServerSyncIssue
                    With syncMismatchWarningDlg
                        .Owner = Me
                        .ShowDialog()
                        RestoreFromServer()  'restarts app when done
                    End With

                    mainStatusInfo.DisplayServerError = True

                    Exit Sub

                End If

                If Not _isRestoring Then

                    DBContext.ServerSynchronization = New ServerSync(DBContext, ServerDBContext)
                    mainStatusInfo.DisplayServerError = False

                    If ServerSync.DatabaseGUID <> "" Then

                        '- standard case: sync all pending items at startup
                        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.Background)
                        Await DBContext.ServerSynchronization.SynchronizeAsync()

                    Else

                        '- connecting current non-demo database for the first time

                        If Not Await FirstTimeConnect() Then

                            'Conflict resolution cancelled or other error: Disconnect from server
                            dlgServerConnection.NewServerContext = Nothing
                            ServerDBContext.Dispose()
                            ServerDBContext = Nothing

                            'server status icon visibilities are data bound to this setting:
                            CustomControls.My.MySettings.Default.IsServerSpecified = False

                        End If

                    End If

                Else

                    _isRestoring = False

                End If

            Else

                'e.g. server unavailable
                ServerDBContext = Nothing
                Protocol_ConnectedChanged(False)

            End If

        Finally

            'startup-critical work involving this handler is done (successfully or not): safe to
            're-allow the native title-bar drag gesture again
            AllowCaptionDrag()

        End Try

    End Sub


    ''' <summary>
    ''' Performs all data binding between data model and UI.
    ''' </summary>
    ''' 
    Private Sub ApplyAllDataBindings(currUser As tblUsers)

        'release any foreign-user experiment preview left over from the previous user's session
        'before switching, so its owner's original pin/display state is restored instead of
        'leaking across users (ExpDisplayList is Nothing on the very first, initial-login call).
        'Not persisted here - DBContext is shared across users, so the in-memory restore is
        'immediately visible to the query below; it's saved by the app's regular save points.

        If ExpDisplayList IsNot Nothing Then
            ReleaseForeignExpPreview()
        End If

        '-- Assign initial contexts

        Me.DataContext = currUser

        ExperimentContent.DbContext = DBContext

        ProtocolItemBase.DbInfo = DBContext.tblDatabaseInfo.First
        pnlInfo.DataContext = Me.DataContext

        '-- Bind experiments tabs to filtered and sorted experiments CollectionViewSource

        Dim res = (From exp In CType(Me.DataContext, tblUsers).tblExperiments Where exp.DisplayIndex IsNot Nothing
                   Order By exp.DisplayIndex Ascending).ToList

        ExpDisplayList = New ObservableCollection(Of tblExperiments)(res)
        tabExperiments.ItemsSource = ExpDisplayList

        ExperimentContent.TabExperimentsPresenter = WPFToolbox.FindVisualChild(Of ContentPresenter)(tabExperiments)

    End Sub


    Private Sub UpdateExperimentTabs(Optional newExpItem As tblExperiments = Nothing)

        'remove doomed items (DisplayIndex = nothing)
        Dim doomedExp = (From exp In ExpDisplayList Where exp.DisplayIndex Is Nothing).FirstOrDefault
        If doomedExp IsNot Nothing Then
            ExpDisplayList.Remove(doomedExp)
        End If

        'replace unpinned experiment
        If newExpItem IsNot Nothing Then
            With ExpDisplayList
                If .Count > 0 AndAlso .Item(0).DisplayIndex = -2 AndAlso newExpItem.DisplayIndex <> -2 Then
                    .Insert(1, newExpItem) 'insert local (or foreign-preview) experiment after server experiment
                Else
                    .Insert(0, newExpItem)
                End If
            End With
        End If

        tabExperiments.UpdateLayout()

    End Sub


    Private Function FilterList(expItem As tblExperiments) As Boolean

        Return expItem.DisplayIndex IsNot Nothing

    End Function


    ''' <summary>
    ''' Releases the currently displayed foreign-user experiment preview tab (DisplayIndex = -3), if any,
    ''' restoring its original owner's DisplayIndex rather than discarding it.
    ''' </summary>
    '''
    Private Sub ReleaseForeignExpPreview()

        Dim previewExp = ExpDisplayList.FirstOrDefault(Function(exp) exp.DisplayIndex.GetValueOrDefault() = -3)
        If previewExp IsNot Nothing Then
            RestoreForeignExpDisplayIndex(previewExp)
            ExpDisplayList.Remove(previewExp)
        End If

    End Sub


    ''' <summary>
    ''' Restores the stashed original DisplayIndex of a foreign-user experiment that was
    ''' temporarily previewed, so its owner's pin/open state reappears correctly for them.
    ''' </summary>
    '''
    Private Sub RestoreForeignExpDisplayIndex(exp As tblExperiments)

        Dim originalIndex As Short? = Nothing
        If ForeignExpOriginalDisplayIndex.TryGetValue(exp, originalIndex) Then
            ForeignExpOriginalDisplayIndex.Remove(exp)
        End If
        exp.DisplayIndex = originalIndex

    End Sub

    ''' <summary>
    ''' Connect to server db for first time: Performs username duplicate checks with server database and offers 
    ''' resolution options if conflicts are found.
    ''' </summary>
    ''' <returns>True, if operation is successful.</returns>
    ''' 
    Private Async Function FirstTimeConnect() As Task(Of Boolean)

        If ServerSync.DatabaseGUID Is Nothing Then  'just to make sure (already checked by calling function)

            Dim conflictingLocalUsers = GetConflictingLocalUsers()
            If conflictingLocalUsers.Count > 0 Then

                'userID conflict(s) found

                Dim serverDbInfoEntry = (From user In ServerDBContext.tblUsers Where user.UserID = conflictingLocalUsers.First.UserID
                                         Select user.Database).First

                Dim resolveConflictDlg As New dlgServerConflict(serverDbInfoEntry)
                With resolveConflictDlg

                    .Owner = Me

                    If .ShowDialog() Then   'currently no option to cancel; if implemented then completely disconnect to prevent loops

                        If .UseRestoreStrategy Then
                            ' Combine server experiments with local ones, then restores from server
                            Return CombineWithServerExperiments(conflictingLocalUsers)  'restarts app if successful
                        Else
                            ' Rename local userID references due conflicting username of other localUser
                            Return RenameLocalUserIDs(conflictingLocalUsers)    'restarts app if successful
                        End If

                    Else

                        cbMsgBox.Display("Server connection cancelled!", MsgBoxStyle.Information, "Phoenix ELN")
                        Return False  'resolution dialog cancelled

                    End If

                End With

            Else

                'no conflicts; bulk upload local database to server (standard case)

                Dim uploadDlg As New dlgUploadProgress(DBContext, ServerDBContext)
                With uploadDlg
                    .Owner = Me
                    .ShowDialog()
                End With

                mainStatusInfo.DisplayServerError = False
                ServerSync.DatabaseGUID = DBContext.tblDatabaseInfo.First.GUID
                Await DBContext.ServerSynchronization.SynchronizeAsync()

                Return True

            End If

        Else

            Return True

        End If

    End Function


    ''' <summary>
    ''' Gets the local localUser entries having identical userID's with the server database. 
    ''' </summary>
    ''' 
    Private Function GetConflictingLocalUsers() As List(Of tblUsers)

        ' Load local users into memory (prevents mixing contexts)
        Dim localUsers = DBContext.tblUsers.ToList()

        'Don't use string.equals() with StringComparison parameter for ignoring case, since this can't be translated to MySQL
        Dim conflictingUsers = localUsers.Where(Function(localUser) _
        ServerDBContext.tblUsers.Any(Function(serverUser) serverUser.UserID.ToLower = localUser.UserID.ToLower)).ToList()

        Return conflictingUsers

    End Function


    ''' <summary>
    ''' Combines the local experiments of a conflicting user with its server experiments. Typical use case when a local user 
    ''' has been created on a new machine with the same userID, instead of restoring from server.
    ''' </summary>
    ''' <returns>True, if successful</returns>
    ''' 
    Private Function CombineWithServerExperiments(duplicateLocalUsers As IEnumerable(Of tblUsers)) As Boolean

        'just to make sure
        If Not duplicateLocalUsers.Any Then
            Return True
        End If

        DBContext.SaveChanges()

        ' increase local experiment sequence numbers to allow seamless subsequent merge with already existing user experiments 
        For Each dupLocalUser In duplicateLocalUsers
            If Not Users.MergeConflictingUserExp(dupLocalUser, DBContext, ServerDBContext) Then
                Return False
            End If
        Next

        ' restore from server
        _isRestoring = True
        Dim serverDBEntry = (From user In ServerDBContext.tblUsers Where user.UserID = duplicateLocalUsers.First.UserID Select user.Database).First
        Dim restoreProgressDlg As New dlgRestoreProgress(ServerDBContext.Database.GetConnectionString, SQLiteDbPath, serverDBEntry.GUID)
        With restoreProgressDlg
            .Owner = Me
            .ShowDialog()
            cbMsgBox.Display("The application now will restart with " + vbCrLf +
                          "the restored database.", MsgBoxStyle.Information, "Restore from Server")
            CustomControls.My.MySettings.Default.RestoreFromServer = True
            Me.Close()
        End With
        _isRestoring = False

        Return True

    End Function


    ''' <summary>
    ''' Interactively assigns new userIDs to all local experiments with conflicting server userIDs.
    ''' </summary>
    ''' 
    Private Function RenameLocalUserIDs(duplicateLocalUsers As IEnumerable(Of tblUsers)) As Boolean

        Dim isFirst As Boolean = True
        Dim localDbGUID = DBContext.tblDatabaseInfo.First.GUID

        For Each dupLocalUser In duplicateLocalUsers

            If Not isFirst Then
                cbMsgBox.Display("Continue to resolve another user-ID conflict:", MsgBoxStyle.Information, "Duplicate Username")
            End If

            Dim dupServerUser = (From user In ServerDBContext.tblUsers Where user.UserID.Equals(dupLocalUser.UserID, StringComparison.CurrentCultureIgnoreCase)).First
            Dim duplicateDlg As New dlgChangeUsername(ServerDBContext, dupServerUser)
            With duplicateDlg
                .Owner = Me
                If .ShowDialog Then
                    If Not Users.ReplaceUserID(DBContext, dupLocalUser, .txtUsername.Text) Then
                        Return False
                    End If
                Else
                    Return False 'cancelled
                End If
            End With

            isFirst = False

        Next

        cbMsgBox.Display("User-ID reassignments complete. The application " + vbCrLf +
               "will restart now and upload your data.", MsgBoxStyle.Information, "Duplicate Resolution")

        CustomControls.My.MySettings.Default.Save()

        'restart application
        Process.Start(Environment.ProcessPath())
        Process.GetCurrentProcess().Kill()

        Return True

    End Function


    Private Sub RestoreFromServer()

        If Not ServerSync.IsSynchronizing Then

            _isRestoring = True

            'display connect dialog if not connected (e.g. demo localUser)
            If ServerDBContext Is Nothing Then
                If Not ConnectToServer(isRestore:=True) Then
                    _isRestoring = False
                    Exit Sub
                End If
            End If

            Dim restoreDlg As New dlgRestoreServer(ServerDBContext)

            With restoreDlg
                .Owner = Me
                .ShowDialog()
                If .TargetDbInfoEntry IsNot Nothing Then
                    Dim restoreProgressDlg As New dlgRestoreProgress(ServerDBContext.Database.GetConnectionString, SQLiteDbPath,
                       .TargetDbInfoEntry.GUID)
                    With restoreProgressDlg
                        .Owner = Me
                        If .ShowDialog() Then
                            'success
                            cbMsgBox.Display("The application now will restart with " + vbCrLf +
                              "the restored database.", MsgBoxStyle.Information, "Restore from Server")
                            CustomControls.My.MySettings.Default.RestoreFromServer = True
                            Me.Close()
                        End If
                    End With
                End If
            End With

            _isRestoring = False

        Else
            cbMsgBox.Display("Retry in a moment, a server sync is currently ongoing!", MsgBoxStyle.Information, "Restore from Server")
        End If

    End Sub

    Private _isRestoring As Boolean = False


    ''' <summary>
    ''' Handles server availability warning display
    ''' </summary>
    ''' 
    Private Sub Protocol_ConnectedChanged(isConnected As Boolean)

        If Not isConnected Then

            Dispatcher.Invoke(Sub() ServerWarningDelegate(isConnected))

            If Not CustomControls.My.MySettings.Default.IsServerOffByUser Then
                cbMsgBox.Display("The ELN server is unavailable!" + vbCrLf + "Changes currently are not backed up. " + vbCrLf + vbCrLf +
                   "Try to reconnect later.", MsgBoxStyle.Information, "Server Sync")
            End If

        Else
            'currently no action
        End If

    End Sub

    Private Sub ServerWarningDelegate(isConnected As Boolean)

        mainStatusInfo.DisplayServerError = Not isConnected

    End Sub


    ''' <summary>
    ''' Handles sync progress for the large tblEmbeddedFiles entries
    ''' </summary>
    ''' 
    Private Sub ServerSync_SyncProgress(progressPercent As Integer)

        '  Debug.WriteLine("Progress: " + progressPercent.ToString)

    End Sub


    ''' <summary>
    ''' Displays the animated ave icon.
    ''' </summary>
    '''
    Private Sub Protocol_RequestSaveIcon(sender As Object)

        mainStatusInfo.AnimateSaveIcon()

    End Sub


    Private Sub UndoRedo_CanUndoChanged(sender As Object, isAvailable As Boolean)
        btnUndo.IsEnabled = isAvailable
    End Sub


    Private Sub UndoRedo_CanRedoChanged(sender As Object, isAvailable As Boolean)
        btnRedo.IsEnabled = isAvailable
    End Sub


    Private Sub ExperimentContent_ContextChanged(sender As Object, newExpEntry As tblExperiments)
        btnClear.IsEnabled = (newExpEntry.WorkflowState <> WorkflowStatus.Finalized)
    End Sub


    ''' <summary>
    ''' Gets the ExperimentContent of the currently selected expTab item.
    ''' </summary>
    ''' 
    Private Function SelectedExpContent() As ExperimentContent

        Dim expContentPresenter = WPFToolbox.FindVisualChild(Of ContentPresenter)(tabExperiments)
        Return WPFToolbox.FindVisualChild(Of ExperimentContent)(expContentPresenter)

    End Function


    Private Sub btnUndo_Click() Handles btnUndo.Click

        With SelectedExpContent()

            .ExpProtocol.Undo()
            .ExpProtocol.RefreshItems()
            .RefreshSketch()
            .UpdateComponentLabels()
            expNavTree.RefreshItems()

        End With

    End Sub


    Private Sub btnRedo_Click() Handles btnRedo.Click

        With SelectedExpContent()

            .ExpProtocol.Redo()
            .ExpProtocol.RefreshItems()
            .RefreshSketch()
            .UpdateComponentLabels()
            expNavTree.RefreshItems()

        End With

    End Sub


    ''' <summary>
    ''' Handles info panel request to restore from server
    ''' </summary>
    '''
    Private Sub DemoStatus_RequestRestoreServer(sender As Object)

        RestoreFromServer()

    End Sub


    ''' <summary>
    ''' Handles info panel request to import a transfer package
    ''' </summary>
    ''' 
    Private Sub DemoStatus_RequestTransferPackage(sender As Object)

        mnuImportPackage_Click()

    End Sub


    Private Sub expNavTree_SelectedUserChanged(sender As Object, currUser As tblUsers) Handles expNavTree.SelectedUserChanged

        'set user as current
        For Each user In DBContext.tblUsers
            user.IsCurrent = 0
        Next
        currUser.IsCurrent = 1

        ApplyAllDataBindings(currUser)

        Dim currExp = (From exp In currUser.tblExperiments Where exp.IsCurrent = 1).FirstOrDefault
        If currExp IsNot Nothing Then
            expNavTree.SelectExperiment(currExp)
        End If

    End Sub


    ''' <summary>
    ''' Handles info panel request to create a first non-demo localUser-
    ''' </summary>
    ''' 
    Private Sub DemoStatus_RequestCreateFirstUser(sender As Object)

        CreateNewUser(isFirstUser:=True)

    End Sub


    Private Sub btnAddUser_Click() Handles btnAddUser.Click

        CreateNewUser(isFirstUser:=False)

    End Sub


    ''' <summary>
    ''' Creates the first non-demo localUser
    ''' </summary>
    ''' 
    Private Sub CreateNewUser(isFirstUser As Boolean)

        If isFirstUser Then

            If Users.CreateFirstUser(Me) Then

                'Connect data model of local database with UI
                Dim currUser = CType(Me.DataContext, tblUsers)
                ApplyAllDataBindings(currUser)

                expNavTree.SelectExperiment(currUser.tblExperiments.First)
                expNavTree.cboUsers.SelectedItem = currUser

                Dim res2 = cbMsgBox.Display("Done! - If the optional Phoenix ELN server " + vbCrLf +
                            "database is installed in your environment," + vbCrLf +
                            "would you like to connect to it now for  " + vbCrLf +
                            "backup and in-house data sharing?",
                            MsgBoxStyle.Information + MsgBoxStyle.YesNo, "Server Connection")
                If res2 = MsgBoxResult.Yes Then
                    ConnectToServer()
                End If

                btnAddUser.IsEnabled = True

            End If

        Else

            Dim newUserEntry = Users.CreateAdditionalUser(Me)

            If newUserEntry IsNot Nothing Then

                SelectedExpContent.ExpProtocol.AutoSave(noUndoPoint:=True)

                ApplyAllDataBindings(newUserEntry)

                'cvs does not update automatically on added items
                Dim cvsSortedUsers As CollectionViewSource = expNavTree.FindResource("cvsSortedUsers")
                cvsSortedUsers.View.Refresh()
                expNavTree.cboUsers.SelectedItem = newUserEntry

                expNavTree.SelectExperiment(newUserEntry.tblExperiments.First)

            End If

        End If

    End Sub


    ''' <summary>
    ''' Handles shortcut keys
    ''' </summary>
    ''' 
    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If Keyboard.Modifiers = ModifierKeys.Control Then

            Dim expState = CType(tabExperiments.SelectedContent, tblExperiments).WorkflowState

            If expState <> WorkflowStatus.Finalized Then

                'only active for non-finalized experiments

                With SelectedExpContent.ExpProtocol
                    Select Case e.Key
                        Case Key.D1
                            .AddRefReactant()
                            e.Handled = True
                        Case Key.D2
                            .AddReagent()
                            e.Handled = True
                        Case Key.D3
                            .AddSolvent()
                            e.Handled = True
                        Case Key.D4
                            .AddAuxiliary()
                            e.Handled = True
                        Case Key.D5
                            .AddProduct()
                            e.Handled = True
                        Case Key.D6
                            .AddComment()
                            e.Handled = True
                        Case Key.D7
                            .AddSeparator()
                            e.Handled = True
                        Case Key.D8
                            .AddFiles()
                            e.Handled = True
                    End Select
                End With

                ' allow protocol-item level undo and redo if no undoable text edit is active

                Dim canUndo As Boolean = False
                Dim focusedBox = Keyboard.FocusedElement
                If TypeOf focusedBox Is TextBox OrElse TypeOf focusedBox Is RichTextBox Then
                    canUndo = CType(focusedBox, Object).CanUndo
                End If

                If Not canUndo Then
                    Select Case e.Key

                        'text edit controls have their own undo/redo
                        Case Key.Z
                            If btnUndo.IsEnabled AndAlso Not CanActiveTextElementUndo() Then
                                btnUndo_Click()
                            End If
                        Case Key.Y
                            If btnRedo.IsEnabled AndAlso Not CanActiveTextElementUndo() Then
                                btnRedo_Click()
                            End If

                    End Select
                End If

            End If

            If e.Key = Key.Return OrElse e.Key = Key.Enter Then
                e.Handled = True
            End If

        Else

            'help key
            If e.Key = Key.F1 Then
                mnuHelp_Click()
            End If

        End If

    End Sub


    ''' <summary>
    ''' Gets if the TextBox or RichTextBox currently in edit mode can perform an undo operation. Always returns  
    ''' false if no text element is in edit mode.
    ''' </summary>
    ''' 
    Private Function CanActiveTextElementUndo() As Boolean

        Dim focusTextElement = Keyboard.FocusedElement

        If TypeOf focusTextElement Is TextBox Then
            Return CType(focusTextElement, TextBox).CanUndo
        ElseIf TypeOf focusTextElement Is RichTextBox Then
            Return CType(focusTextElement, RichTextBox).CanUndo
        Else
            Return False
        End If

    End Function


    ''' <summary>
    ''' Gets if the TextBox or RichTextBox currently in edit mode can perform an undo operation. Always returns  
    ''' false if no text element is in edit mode.
    ''' </summary>
    ''' 
    Private Function CanActiveTextElementRedo() As Boolean

        Dim focusTextElement = Keyboard.FocusedElement

        If TypeOf focusTextElement Is TextBox Then
            Return CType(focusTextElement, TextBox).CanRedo
        ElseIf TypeOf focusTextElement Is RichTextBox Then
            Return CType(focusTextElement, RichTextBox).CanRedo
        Else
            Return False
        End If

    End Function


    ''' <summary>
    ''' Validates the content of a TreeViewItemLabel or a ListBoxEditLabel if the mouse currently is over, and 
    ''' commits their content to the database if valid. Otherwise false is returned to allow to block 
    ''' subsequent actions.
    ''' </summary>
    ''' <remarks>Example: A project name currently being edited in the experiments tree project node needs 
    ''' to be checked for duplicates when an application exit is requested in the midst of the edit. </remarks>
    ''' 
    Private Function ValidateAndCommitOpenEditControls() As Boolean

        ' TreeViewEditLabel
        '------------------
        If TreeViewEditLabel.CurrentEditItem IsNot Nothing Then
            If Not TreeViewEditLabel.CurrentEditItem.IsMouseOver Then
                'mouse is not over active edit label
                Return TreeViewEditLabel.CurrentEditItem.EndEdit  'also performs validation
            Else
                'same item: skip validation and EndEdit
                Return True
            End If
        End If

        ' LisBoxEditLabel
        '----------------
        If ListBoxEditLabel.CurrentEditItem IsNot Nothing Then
            If Not ListBoxEditLabel.CurrentEditItem.IsMouseOver Then
                ' mouse not over active edit tvItem
                Return ListBoxEditLabel.CurrentEditItem.EndEdit  'also performs validation
            Else
                'same item: skip validation and EndEdit
                Return True
            End If
        End If

        Return True

    End Function


    Private Sub Me_PreviewMouseLeftButtonDown(sender As Object, e As RoutedEventArgs) Handles Me.PreviewMouseDown

        If Not ValidateAndCommitOpenEditControls() Then
            e.Handled = True
        End If

    End Sub


    Private Sub Me_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing

        If Not ValidateAndCommitOpenEditControls() Then
            e.Cancel = True
            Exit Sub
        End If

        'wait for server sync to complete
        If ServerSync.IsSynchronizing Then
            Dim syncProgressDlg As New dlgSyncProgress
            With syncProgressDlg
                .Owner = Me
                .ShowDialog()
            End With
        End If

        'in case of some temp file leftovers
        FileContent.CleanupTempFolderDocs()

        'save app window settings
        With CustomControls.My.MySettings.Default
            .StartupPosition = New System.Drawing.Point(Left, Top)
            .StartupSize = New System.Drawing.Size(Width, Height)
            .Save()
        End With

        'restore any still-open foreign-user experiment preview, so its owner's pin/open state isn't left stuck
        ReleaseForeignExpPreview()

        'final save
        DBContext.SaveChanges()

        'restart ELN if restoring from server, or migrating data
        If CustomControls.My.MySettings.Default.RestoreFromServer = True OrElse CustomControls.My.MySettings.Default.DataMigrationType Then
            Process.Start(Environment.ProcessPath())
            Process.GetCurrentProcess().Kill()
        End If

    End Sub


    Private Sub Warning_Delegate()

        cbMsgBox.Display("The demo experiments limit is reached." + vbCrLf + vbCrLf +
               "Click the green 'Create User' button" + vbCrLf +
               "to start with a unique user now.", MsgBoxStyle.Information, "Demo Validation")

    End Sub


    ''' <summary>
    ''' Add or clone a new experiment
    ''' </summary>
    '''
    Private Sub expNavTree_RequestAddExperiment(sender As Object, folderEntry As tblProjFolders) Handles expNavTree.RequestAddExperiment

        Dim userEntry = CType(Me.DataContext, tblUsers)

        '- handle demo experiments limit

        If userEntry.UserID = "demo" Then

            Dim demoConv As New RemainingDemoCountConverter
            Dim remainingDemo = demoConv.Convert(userEntry.tblExperiments.Count, Nothing, Nothing, Nothing)
            If remainingDemo <= 0 Then
                Dispatcher.Invoke(Sub() Warning_Delegate())
                Exit Sub
            End If

        End If

        If SelectedExpContent.CreateExperiment(DBContext, expNavTree, folderEntry.Project, folderEntry) Then

            pnlInfo.DataContext = Nothing
            pnlInfo.DataContext = CType(Me.DataContext, tblUsers)

        End If

    End Sub


    ''' <summary>
    ''' Handles experiment selection within RSS results and step summary control.
    ''' </summary>
    ''' 
    Private Sub ResultList_RequestOpenExperiment(sender As Object, targetExp As tblExperiments, isFromServer As Boolean)
        TryOpenExperiment(targetExp, isFromServer)
    End Sub


    Private Sub StepExpSelector_RequestOpenExperiment(sender As Object, targetExp As tblExperiments, isFromServer As Boolean, args As StepExpOpenArgs)
        args.WasOpened = TryOpenExperiment(targetExp, isFromServer)
    End Sub


    Private Sub RssItemGroup_RequestOpenExperiment(sender As Object, targetExp As tblExperiments, isFromServer As Boolean, args As StepExpOpenArgs)
        args.WasOpened = TryOpenExperiment(targetExp, isFromServer)
    End Sub


    Private Sub FullTextSearch_RequestOpenExperiment(sender As Object, targetExp As tblExperiments, isFromServer As Boolean, args As StepExpOpenArgs)
        args.WasOpened = TryOpenExperiment(targetExp, isFromServer)
    End Sub


    Private Function TryOpenExperiment(targetExp As tblExperiments, isFromServer As Boolean) As Boolean

        If targetExp Is Nothing Then
            Return False
        End If

        If Not isFromServer Then

            ''-- local experiment and same current user as of targetExp
            expNavTree.SelectExperiment(targetExp)
            Return True

        Else

            '-- server experiment

            If ExpDisplayList.Contains(targetExp) Then

                ' experiment already displayed in a tab
                Dim thisTab As TabItem = tabExperiments.ItemContainerGenerator.ContainerFromItem(targetExp)
                If thisTab Is Nothing Then
                    tabExperiments.UpdateLayout()
                    thisTab = tabExperiments.ItemContainerGenerator.ContainerFromItem(targetExp)
                End If
                If thisTab IsNot Nothing Then
                    thisTab.IsSelected = True
                End If
                Return True

            Else

                'if server target exp originates from current user, then open it from *local* database
                If targetExp.UserID = CType(Me.DataContext, tblUsers).UserID Then
                    Dim localExp = DBContext.tblExperiments.Where(Function(exp) exp.ExperimentID = targetExp.ExperimentID).FirstOrDefault
                    If localExp IsNot Nothing Then
                        expNavTree.SelectExperiment(localExp)
                        Return True
                    End If
                End If

                ' warn about too many open exp tabs
                If tabExperiments.Items.Count > 4 AndAlso Not ExpDisplayList.First.DisplayIndex = -2 Then
                    Dim res = cbMsgBox.Display("The maximum of 5 open experiments will be" + vbCrLf +
                                    "exceeded if opening the server experiment." + vbCrLf + vbCrLf +
                                    "Release the rightmost experiment?", MsgBoxStyle.OkCancel + MsgBoxStyle.Information, "Pin Limit")
                    If res = MsgBoxResult.Ok Then
                        Dim lastIndex = tabExperiments.Items.Count - 1
                        CType(tabExperiments.Items(lastIndex), tblExperiments).DisplayIndex = Nothing
                    Else
                        Return False
                    End If
                End If

                'replace previous leftmost server exp, if present
                Dim leftmostExp = CType(tabExperiments.Items(0), tblExperiments)
                If leftmostExp.DisplayIndex = -2 Then
                    leftmostExp.DisplayIndex = Nothing
                End If

                ''add to experiments tab
                targetExp.DisplayIndex = -2
                UpdateExperimentTabs(targetExp)

                'select leftmost experiments tab containing this server exp
                tabExperiments.SelectedIndex = 0
                Return True

            End If

        End If

    End Function


    ''' <summary>
    ''' Tries to reconnect to the server after the connection was lost.
    ''' </summary>
    ''' 
    Private Sub mainStatusInfo_RequestReconnect() Handles mainStatusInfo.RequestReconnect

        Me.Cursor = Cursors.Wait
        Me.ForceCursor = True

        With CustomControls.My.MySettings.Default
            If .IsServerSpecified Then
                .IsServerOffByUser = False
                DbUpgradeServer.IsUpgradeCheckRequired = True
                ServerSync.CreateServerContextAsync(.ServerName, .ServerDbUserName, .ServerDbPassword, .ServerPort,
                    DBContext.tblDatabaseInfo.First) 'handled by ServerSync_ServerContextCreated (also sets mainStatusInfo)
            End If
        End With

    End Sub


    ''' <summary>
    ''' Handles experiment selection within experiment navigation tree.
    ''' </summary>
    ''' 
    Private Sub expNavTree_ExperimentSelected(sender As Object, selectedItem As Object) Handles expNavTree.ExperimentSelected

        If TypeOf selectedItem Is tblExperiments Then

            Dim selExp = CType(selectedItem, tblExperiments)

            If ExpDisplayList.Contains(selExp) Then

                '- experiment already displayed in a tab

                Dim thisTab As TabItem = tabExperiments.ItemContainerGenerator.ContainerFromItem(selExp)
                If thisTab Is Nothing Then
                    tabExperiments.UpdateLayout()
                    thisTab = tabExperiments.ItemContainerGenerator.ContainerFromItem(selExp)
                End If
                If thisTab IsNot Nothing Then
                    thisTab.IsSelected = True
                End If

            Else

                '- experiment not yet present in tab

                If tabExperiments.Items.Count > 0 Then

                    Dim leftmostExp = CType(tabExperiments.Items(0), tblExperiments)
                    Dim localStartIndex = If(leftmostExp.DisplayIndex = -2, 1, 0)   'displayIndex = -2 means server experiment (leftmost)
                    Dim firstLocalExp = CType(tabExperiments.Items(localStartIndex), tblExperiments)

                    If firstLocalExp IsNot Nothing Then

                        If selExp.UserID = CType(Me.DataContext, tblUsers).UserID Then

                            Select Case firstLocalExp.DisplayIndex

                                Case 0

                                    'replace current leftmost exp tab
                                    firstLocalExp.DisplayIndex = Nothing

                                Case -1

                                    'leftmost was pinned
                                    For i = localStartIndex + 1 To tabExperiments.Items.Count - 1
                                        Dim expItem = CType(tabExperiments.Items(i), tblExperiments)
                                        expItem.DisplayIndex += 1
                                    Next
                                    firstLocalExp.DisplayIndex = 1

                            End Select

                            selExp.DisplayIndex = 0

                        Else

                            'experiment owned by another local user (e.g. opened from a cross-user search
                            'result): never overwrite the owner's real pin/display state, and never replace
                            'or shift the current user's own tabs - just insert it as a transient preview
                            'tab to the left of them instead
                            ReleaseForeignExpPreview()
                            ForeignExpOriginalDisplayIndex(selExp) = selExp.DisplayIndex
                            selExp.DisplayIndex = -3

                        End If

                        UpdateExperimentTabs(selExp)
                        DBContext.SaveChanges()     'no exp-level undo/redo required

                    End If

                    tabExperiments.SelectedIndex = localStartIndex 'select first tab

                Else

                    ' no experiments present: rare special situation, but needs to be handled
                    Try
                        If selExp.UserID = CType(Me.DataContext, tblUsers).UserID Then
                            selExp.DisplayIndex = 0
                        Else
                            ForeignExpOriginalDisplayIndex(selExp) = selExp.DisplayIndex
                            selExp.DisplayIndex = -3
                        End If
                        UpdateExperimentTabs(selExp)
                        DBContext.SaveChanges()     'no exp-level undo/redo required
                        tabExperiments.SelectedIndex = 0
                    Catch ex As Exception
                        'do nothing
                    End Try

                End If

            End If
        End If

    End Sub


    ''' <summary>
    ''' Handles changes to the current experiment tab. 
    ''' </summary>
    '''
    Private Sub expTabHeader_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)   'XAML event

        'remember current scroll position

        Dim thisTab As TabItem = tabExperiments.ItemContainerGenerator.ContainerFromItem(tabExperiments.SelectedItem)
        If thisTab IsNot Nothing Then
            Dim tabItemInfo = CType(thisTab.Tag, TabItemInfo)
            tabItemInfo.VerticalScrollPos = SelectedExpContent.ExpProtocol.VerticalScrollOffset
        End If

        'change to target experiment tab
        Dim currExp = CType(sender.DataContext, tblExperiments)
        If currExp.DisplayIndex <> -2 Then
            'local experiments only (not server)
            expNavTree.SelectExperiment(currExp, populateContent:=False)
        End If

    End Sub


    Private Sub expTabHeader_PinStateChanged(sender As Object, targetExp As tblExperiments)

        Select Case targetExp.DisplayIndex

            Case -2

                'release server experiment
                targetExp.DisplayIndex = Nothing
                tabExperiments.SelectedIndex = 1

            Case -3

                'release foreign local user experiment
                RestoreForeignExpDisplayIndex(targetExp)
                ExpDisplayList.Remove(targetExp)
                tabExperiments.SelectedIndex = 0

            Case -1

                'undo pre-pin of leftmost local experiment
                targetExp.DisplayIndex = 0

            Case 0

                If tabExperiments.Items.Count < 5 Then

                    'pre-pin leftmost local experiment
                    targetExp.DisplayIndex = -1

                Else

                    cbMsgBox.Display("Can't pin this experiment, since the maximum " + vbCrLf +
                            "number of 4 pinned experiments already is" + vbCrLf +
                            "present.",
                            MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Pin Limit")
                End If

            Case Else

                'release pinned experiment

                Dim doomedIndex = targetExp.DisplayIndex
                targetExp.DisplayIndex = Nothing

                'renumber DisplayIndexes of exp to the right of the doomed one
                Dim res = From exp In ExpDisplayList Where exp.DisplayIndex > doomedIndex
                For Each exp In res
                    exp.DisplayIndex -= 1
                Next

                'is selected tab doomed one? -> remove and select neighbor to its left
                If CType(tabExperiments.SelectedItem, tblExperiments) Is targetExp Then
                    Dim expToLeft = CType(tabExperiments.Items(doomedIndex - 1), tblExperiments)
                    If expToLeft.IsCurrent = 0 Then
                        expNavTree.SelectExperiment(expToLeft, False)
                        tabExperiments.SelectedIndex = doomedIndex - 1
                    End If
                End If

        End Select

        UpdateExperimentTabs()

        DBContext.SaveChanges()

    End Sub


    Private Sub expNavTree_RequestLocateCurrentExperiment(sender As Object) Handles expNavTree.RequestLocateCurrentExperiment

        Dim currExp = TryCast(SelectedExpContent()?.DataContext, tblExperiments)
        If currExp IsNot Nothing Then
            expNavTree.ScrollExperimentIntoView(currExp)
        End If

    End Sub


    Private Sub btnPrint_Click() Handles btnPrint.Click

        ExperimentPrint.Print(SelectedExpContent, False, Me)

    End Sub


    Private Sub btnPDF_Click() Handles btnPDF.Click

        ExperimentPrint.Print(SelectedExpContent, True, Me)

    End Sub


    Private Sub btnClear_Click() Handles btnClear.Click

        If CType(SelectedExpContent.DataContext, tblExperiments).WorkflowState <> WorkflowStatus.Finalized Then

            Dim res = cbMsgBox.Display("Do you really want to delete all protocol" + vbCrLf +
                         "entries, except the reference reactant?",
                        MsgBoxStyle.OkCancel + MsgBoxStyle.DefaultButton2 + MsgBoxStyle.Exclamation, "Clear Protocol")

            If res = MsgBoxResult.Ok Then
                SelectedExpContent.ExpProtocol.ClearItems()
            End If

        End If

    End Sub


    Private Sub btnExport_Click() Handles btnExport.Click

        Dim currExp = CType(SelectedExpContent.DataContext, tblExperiments)
        Dim lastExportDir = CustomControls.My.MySettings.Default.LastExportDir

        Dim saveFileDlg As New SaveFileDialog
        With saveFileDlg

            If lastExportDir = "" Then
                .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            Else
                .InitialDirectory = lastExportDir
            End If

            .FileName = currExp.ExperimentID + ".exp"
            .Filter = "ELN export file (*.exp)|*.exp"
            .Title = "Export current experiment as file ..."

            If .ShowDialog Then
                ExperimentBase.ExportExperiment(currExp, .FileName, ApplicationVersion.ToString, DBContext)
                CustomControls.My.MySettings.Default.LastExportDir = IO.Path.GetDirectoryName(.FileName)
            End If

        End With

    End Sub


    Private Sub btnEditUser_Click() Handles btnEditUser.Click

        Dim newUserDlg As New dlgNewUser(DBContext, ServerDBContext)

        With newUserDlg
            .Owner = Me
            .Title = "User Info"
            .CurrentUser = CType(Me.DataContext, tblUsers)  'specifying a localUser means to edit its data
            If .ShowDialog() Then

                With .CurrentUser
                    .FirstName = newUserDlg.txtFirstName.Text
                    .LastName = newUserDlg.txtLastName.Text
                    .CompanyName = newUserDlg.txtOrganization.Text
                    .City = newUserDlg.txtSite.Text
                    .DepartmentName = newUserDlg.txtDepartment.Text
                End With

                DBContext.SaveChanges() 'no auto-save, since not occurring at experiment level

            End If
        End With

    End Sub


    Private Sub btnConnect_Click() Handles btnConnect.Click

        ConnectToServer()

    End Sub


    Private Sub btnRestore_Click() Handles btnRestore.Click

        RestoreFromServer()

    End Sub


    Private Sub mnuCreatePackage_Click() Handles mnuCreatePackage.Click

        Dim currUserID = CType(Me.DataContext, tblUsers).UserID
        TransferPackage.Create(currUserID)

    End Sub


    Private Sub mnuImportPackage_Click() Handles mnuImportPackage.Click

        Dim isDemo As Boolean = (CType(Me.DataContext, tblUsers).UserID = "demo")

        If TransferPackage.InitializeImport(isDemo) Then

            Select Case CustomControls.My.MySettings.Default.DataMigrationType

                Case MigrationType.ReplaceProductive
                    cbMsgBox.Display("Transfer package successfully imported!" + vbCrLf +
                        "The application will restart now to complete the data migration.", MsgBoxStyle.Information + MsgBoxStyle.OkOnly, "Transfer Package")
                Case Else
                    cbMsgBox.Display("Recovery file successfully imported!" + vbCrLf +
                        "The application will restart now to complete the recovery.", MsgBoxStyle.Information + MsgBoxStyle.OkOnly, "Transfer Package")
            End Select

            Me.Close()

        End If

    End Sub


    Private Sub mnuHelp_Click() Handles mnuHelp.Click

        Dim info As New ProcessStartInfo("https://abrechts.github.io/phoenix-eln-help.github.io/pages/1CreateExperiment.html") With {
            .UseShellExecute = True
        }
        Process.Start(info)

    End Sub


    Private Sub mnuAbout_Click() Handles mnuAbout.Click

        Dim aboutDlg As New dlgAbout
        With aboutDlg
            .Owner = Me
            .blkAppVersion.Text = ApplicationVersion.ToString(3)
            .ShowDialog()
        End With

    End Sub


    Private Async Sub mnuCheckForUpdates_Click() Handles mnuCheckForUpdates.Click

        Dim newVersionStr = Await PhpServices.GetLatestAppVersionAsync
        If newVersionStr = "" Then
            cbMsgBox.Display("Sorry, can 't access version server.", MsgBoxStyle.Information, "Update Check")
            Exit Sub 'server error
        End If

        Dim latestVersion = New Version(newVersionStr)
        If ApplicationVersion < latestVersion Then
            pnlStatus.ShowAvailableUpdate(newVersionStr)
            cbMsgBox.Display("An update is available! See the" + vbCrLf +
                   "info panel for details ...", MsgBoxStyle.Information, "Update Check")
        Else
            cbMsgBox.Display("Your application is up-to-date!", MsgBoxStyle.Information, "Update Check")
        End If

    End Sub


    Private Sub mnuSearchRSS_Click() Handles mnuSearchRSS.MouseUp

        Dim searchDlg As New dlgSearch
        With searchDlg

            .LocalDBContext = DBContext
            .ServerDBContext = ServerDBContext

            Dim settings = CustomControls.My.MySettings.Default
            If settings.dlgSearchSize.Width > -1 Then
                .WindowStartupLocation = WindowStartupLocation.Manual
                .Left = settings.dlgSearchPosition.X
                .Top = settings.dlgSearchPosition.Y
                .Width = settings.dlgSearchSize.Width
                .Height = settings.dlgSearchSize.Height
            Else
                .Owner = Me
            End If

            .ShowDialog()

        End With

    End Sub


    Private Sub mnuFullTextSearch_Click() Handles mnuSearchFullText.MouseUp

        Dim searchDlg As New dlgFullTextSearch With {
            .LocalDBContext = DBContext,
            .ServerDBContext = ServerDBContext,
            .Owner = Me
        }

        searchDlg.ShowDialog()

    End Sub



    Private Sub ExperimentContent_RequestSequencesDialog(sender As Object, refExp As tblExperiments, isServerExp As Boolean)

        If refExp.RxnSketch Is Nothing Then
            cbMsgBox.Display("Missing reaction sketch: Can't create a connection graph!", MsgBoxStyle.OkOnly, "Synthetic Connections")
            Exit Sub
        End If

        If isServerExp AndAlso ServerDBContext Is Nothing Then
            cbMsgBox.Display("The current experiment originates from the server, but the server currently is unavailable!", MsgBoxStyle.OkOnly)
            Exit Sub
        End If

        Dim connectionsDlg As New dlgConnectGraph(refExp, DBContext, ServerDBContext, isServerExp OrElse CustomControls.My.MySettings.Default.UseServerSequences)

        With connectionsDlg

            Dim settings = CustomControls.My.MySettings.Default
            If settings.dlgSequencesSize.Width > -1 Then
                .WindowStartupLocation = WindowStartupLocation.Manual
                .Left = settings.dlgSequencesPosition.X
                .Top = settings.dlgSequencesPosition.Y
                .Width = settings.dlgSequencesSize.Width
                .Height = settings.dlgSequencesSize.Height
            Else
                .Owner = Me
            End If

            .ShowDialog()

            settings.dlgSequencesPosition = New System.Drawing.Point(.Left, .Top)
            settings.dlgSequencesSize = New System.Drawing.Size(.ActualWidth, .ActualHeight)

        End With

    End Sub


    ''' <summary>
    ''' Displays the Structure Graph (dlgSequenceScheme) directly for the given experiment, without
    ''' going through the Connection Graph dialog first.
    ''' </summary>
    '''
    Private Sub ExperimentContent_RequestStructureGraph(sender As Object, refExp As tblExperiments, isServerExp As Boolean)

        If refExp.RxnSketch Is Nothing Then
            cbMsgBox.Display("Missing reaction sketch: Can't create a structure graph!", MsgBoxStyle.OkOnly, "Synthetic Connections")
            Exit Sub
        End If

        Dim useServer = isServerExp OrElse CustomControls.My.MySettings.Default.UseServerSequences

        If useServer AndAlso ServerDBContext Is Nothing Then
            cbMsgBox.Display("The current experiment originates from the server, but the server currently is unavailable!", MsgBoxStyle.OkOnly)
            Exit Sub
        End If

        Dim queryExperiments = If(useServer, ServerDBContext.tblExperiments, DBContext.tblExperiments)

        Dim sequences = dlgConnectGraph.BuildSequences(refExp, queryExperiments)

        Dim schemeDlg As New dlgSequenceScheme()
        With schemeDlg
            .Owner = Me
            .SetData(sequences, queryExperiments, refExp, DBContext.tblExperiments,
                     If(ServerDBContext IsNot Nothing, ServerDBContext.tblExperiments, Nothing))
            .Show()
        End With

    End Sub


    Private Sub mnuSpellOff_Click() Handles mnuSpellOff.Click
        SpellChecker.IsEnabled = False
        txtSpellHeader.Text = mnuSpellOff.Header
        txtSpellHeader.Tag = mnuSpellOff.Tag
        txtSpellHeader.ToolTip = "No spell checking"
        CustomControls.My.MySettings.Default.LastSpellCheckLocale = ""
    End Sub

    Private Sub mnuSpellDE_Click() Handles mnuSpellDE.Click
        SpellChecker.ChangeLocale("de-DE")
        txtSpellHeader.Text = mnuSpellDE.Header
        txtSpellHeader.ToolTip = mnuSpellDE.ToolTip
        CustomControls.My.MySettings.Default.LastSpellCheckLocale = "de-DE"
    End Sub

    Private Sub mnuSpellEnUS_Click() Handles mnuSpellEnUS.Click
        SpellChecker.ChangeLocale("en-US")
        txtSpellHeader.Text = mnuSpellEnUS.Header
        txtSpellHeader.ToolTip = mnuSpellEnUS.ToolTip
        CustomControls.My.MySettings.Default.LastSpellCheckLocale = "en-US"
    End Sub

    Private Sub mnuSpellEnGB_Click() Handles mnuSpellEnGB.Click
        SpellChecker.ChangeLocale("en-GB")
        txtSpellHeader.Text = mnuSpellEnGB.Header
        txtSpellHeader.ToolTip = mnuSpellEnGB.ToolTip
        CustomControls.My.MySettings.Default.LastSpellCheckLocale = "en-GB"
    End Sub

    Private Sub mnuSpellFR_Click() Handles mnuSpellFR.Click
        SpellChecker.ChangeLocale("fr-FR")
        txtSpellHeader.Text = mnuSpellFR.Header
        txtSpellHeader.ToolTip = mnuSpellFR.ToolTip
        CustomControls.My.MySettings.Default.LastSpellCheckLocale = "fr-FR"
    End Sub

    Private Sub mnuSpellES_Click() Handles mnuSpellES.Click
        SpellChecker.ChangeLocale("es-ES")
        txtSpellHeader.Text = mnuSpellES.Header
        txtSpellHeader.ToolTip = mnuSpellES.ToolTip
        CustomControls.My.MySettings.Default.LastSpellCheckLocale = "es-ES"
    End Sub

    Private Sub mnuSpellIT_Click() Handles mnuSpellIT.Click
        SpellChecker.ChangeLocale("it-IT")
        txtSpellHeader.Text = mnuSpellIT.Header
        txtSpellHeader.ToolTip = mnuSpellIT.ToolTip
        CustomControls.My.MySettings.Default.LastSpellCheckLocale = "it-IT"
    End Sub

    Private Sub mnuSpellPT_Click() Handles mnuSpellPT.Click
        SpellChecker.ChangeLocale("pt-PT")
        txtSpellHeader.Text = mnuSpellPT.Header
        txtSpellHeader.ToolTip = mnuSpellPT.ToolTip
        CustomControls.My.MySettings.Default.LastSpellCheckLocale = "pt-PT"
    End Sub

    Private Sub mnuDict_Click() Handles mnuDict.Click

        Dim ignoredWords = SpellChecker.CustomIgnoredWords
        ignoredWords.Sort()

        Dim customWordsDlg As New dlgCustomDictionary(ignoredWords)
        With customWordsDlg
            .Owner = Me
            .ShowDialog()
        End With

    End Sub


    ''' <summary>
    ''' Displays server connect dialog and return true if the connection succeeded
    ''' </summary>
    ''' 
    Friend Function ConnectToServer(Optional isRestore As Boolean = False) As Boolean

        If CType(Me.DataContext, tblUsers).UserID = "demo" AndAlso Not isRestore Then

            cbMsgBox.Display("Sorry, the 'demo' user can't connect " + vbCrLf +
                   "to the server database.", MsgBoxStyle.Information, "Server Connect")
            Return False

        End If

        Dim connDlg As New dlgServerConnection(DBContext, ServerDBContext)

        With connDlg
            .Owner = Me

            If .ShowDialog() Then

                If dlgServerConnection.NewServerContext IsNot Nothing Then

                    '-- apply new server context
                    ServerDBContext = dlgServerConnection.NewServerContext
                    DBContext.ServerSynchronization = New ServerSync(DBContext, ServerDBContext)

                    mainStatusInfo.DisplayServerError = False

                    Return True
                Else

                    '-- disconnect
                    If CustomControls.My.MySettings.Default.IsServerSpecified Then
                        If ServerDBContext IsNot Nothing Then

                            mainStatusInfo.DisplayServerError = True

                            ServerDBContext.Dispose()
                            ServerDBContext = Nothing

                        End If
                    End If
                    Return False
                End If
            Else
                '-- cancel
                Return False
            End If

        End With

    End Function



    'DEVELOPMENT ONLY!! Reset and re-upload server DB
    '------------------------------------------------

    Private Sub TestResetAndUpload()

        'ServerDBContext.Database.EnsureDeleted()
        'ServerDBContext.Database.EnsureCreated()
        'Dim uploadDlg As New dlgUploadProgress(DBContext, ServerDBContext)
        'With uploadDlg
        '    .Owner = Me
        '    .ShowDialog()
        'End With
        'Dim sync As New ServerSync(DBContext, ServerDBContext)
        'sync.ResetSyncFlags()


        'For TESTING server-side experiments only!
        '---------------------------------------------
        'ServerSync.IsConnected = False
        'LocalDBContext = serverContext
        'Me.DataContext = ServerDBContext.tblUsers.First
        'ExperimentContent.DbContext = ServerDBContext
        'ProtocolItemBase.DbInfo = ServerDBContext.tblDatabaseInfo.First

    End Sub

End Class

