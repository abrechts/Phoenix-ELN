Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports CustomControls
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Win32

Class MainWindow

    Friend Shared Property DBContext As ElnDbContext

    Friend Shared Property ServerDBContext As ElnDbContext

    Friend Shared Property SQLiteDbPath As String

    Friend Shared Property ApplicationVersion As Version

    Private Property ExpDisplayList As ObservableCollection(Of tblExperiments)

    Private _IsVersionUpgrade As Boolean = False


    Public Sub New()

        InitializeComponent()

        'Prevent another instance of the application to be run
        If (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1) Then
            Me.Hide()
            MsgBox("Phoenix ELN is already running! ", MsgBoxStyle.Information, "Phoenix ELN")
            Me.Close()
            Application.Current.Shutdown()
        End If

        SQLiteDbPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\Phoenix ELN Data\ElnData.db"

        'Replace local database by demo database, if missing for some reason
        If Not File.Exists(SQLiteDbPath) Then
            File.Copy("DB-Seed\ElnData.db", SQLiteDbPath)
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

            'Apply settings
            If .StartupSize.Width > -1 Then
                WindowStartupLocation = WindowStartupLocation.Manual
                Left = .StartupPosition.X
                Top = .StartupPosition.Y
                Width = .StartupSize.Width
                Height = .StartupSize.Height
            End If

            'Check for pending restore
            If .RestoreFromServer = True Then
                Threading.Thread.Sleep(50)  'allow previous instance to close
                Dim tempPath = IO.Path.GetDirectoryName(SQLiteDbPath) + "\ElnData.tmp"
                Try
                    File.Move(tempPath, SQLiteDbPath, True)    'overwrite current working db
                Catch ex As Exception
                    MsgBox("Can't replace current experiments database for " + vbCrLf +
                        "restore, since locked by another application!", MsgBoxStyle.Information, "Restore Error")
                End Try
                .RestoreFromServer = False
                .Save()
            End If

        End With

        'Apply database upgrades for new version if required
        If _IsVersionUpgrade Then
            DbUpgradeServer.IsNewAppVersion = True
            DbUpgradeLocal.Upgrade(SQLiteDbPath)
        End If

        'Create local SqliteContext
        DBContext = New SQLiteContext(SQLiteDbPath).ElnContext

        'Check for legacy Rss backlog registration (--> can be removed later!)
        If New Version(DBContext.tblDatabaseInfo.First.CurrAppVersion) < New Version("0.9.5") Then
            Dim rss As New RxnSubstructure
            rss.RegisterRssBacklog(DBContext.tblDatabaseInfo.First.tblUsers.First)
            DBContext.SaveChanges()
        End If

        ApplicationVersion = GetType(MainWindow).Assembly.GetName().Version
        DBContext.tblDatabaseInfo.First.CurrAppVersion = ApplicationVersion.ToString

        'Version update: Send install statistics
        If _IsVersionUpgrade Then
            PhpServices.SendInstallInfo(ApplicationVersion.ToString(3), DBContext, CustomControls.My.MySettings.Default.IsServerSpecified)
        End If

        RemainingDemoCountConverter.MaxDemoCount = 15
        RemainingDemoCountConverter.FactoryDemoCount = 6

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        'register shared events
        AddHandler Protocol.RequestSaveIcon, AddressOf Protocol_RequestSaveIcon
        AddHandler UndoRedo.CanUndoChanged, AddressOf UndoRedo_CanUndoChanged
        AddHandler UndoRedo.CanRedoChanged, AddressOf UndoRedo_CanRedoChanged
        AddHandler StatusDemo.RequestCreateFirstUser, AddressOf DemoStatus_RequestCreateFirstUser
        AddHandler StatusDemo.RequestRestoreServer, AddressOf DemoStatus_RequestRestoreServer
        AddHandler ServerSync.ServerContextCreated, AddressOf ServerSync_ServerContextCreated
        AddHandler Protocol.ConnectedChanged, AddressOf Protocol_ConnectedChanged
        AddHandler ServerSync.SyncProgress, AddressOf ServerSync_SyncProgress
        AddHandler dlgServerConnection.ServerContextCreated, AddressOf ServerSync_ServerContextCreated
        AddHandler ExpTabHeader.PinStateChanged, AddressOf expTabHeader_PinStateChanged
        AddHandler StepSummary.RequestOpenExperiment, AddressOf ExpList_RequestOpenExperiment
        AddHandler RssItemGroup.RequestOpenExperiment, AddressOf ExpList_RequestOpenExperiment
        AddHandler StepExpSelector.RequestOpenExperiment, AddressOf ExpList_RequestOpenExperiment


        'Connect local database model with UI
        ApplyAllDataBindings()

        'create server context async to reduce startup time
        With CustomControls.My.MySettings.Default

            If DBContext.tblDatabaseInfo.First.tblUsers.First.UserID <> "demo" Then
                If .IsServerSpecified Then
                    If Not .IsServerOffByUser Then

                        If .IsServerQuery Then
                            'disable search until server conn established
                            btnSearch.IsEnabled = False
                        End If

                        ServerSync.CreateServerContextAsync(.ServerName, .ServerDbUserName, .ServerDbPassword, .ServerPort,
                           DBContext.tblDatabaseInfo.First) 'handled by ServerSync_ServerContextCreated (also sets mainStatusInfo)

                    Else
                        'manually disconnected by user
                        mainStatusInfo.DisplayServerError = True
                    End If
                End If
            Else
                'demo user has no server connection
                .IsServerSpecified = False 'visibility of server status items is data bound to this setting
            End If

        End With

        'select experiment tab of current experiment (may be a pinned one)
        Dim currUser = CType(Me.DataContext, tblUsers)
        Dim currExp = (From exp In currUser.tblExperiments Where exp.IsCurrent).FirstOrDefault

        If currExp IsNot Nothing Then
            Dim thisTab As TabItem = tabExperiments.ItemContainerGenerator.ContainerFromItem(currExp)
            If thisTab IsNot Nothing Then
                thisTab.IsSelected = True
            End If
        End If

        'start the periodic cleanup process for embedded document editing resources (currently set to every hour)
        FileContent.StartDocEditCleanupTimer(New TimeSpan(1, 0, 0))

        'check for updates async
        CheckForUpdatesAsync()

    End Sub


    Private Sub ServerSync_ServerContextCreated(serverContext As ElnDbContext)

        Me.Cursor = Cursors.Arrow
        Me.ForceCursor = False

        btnSearch.IsEnabled = True

        If serverContext IsNot Nothing Then

            '- handle syncID mismatch

            ServerDBContext = serverContext

            If ServerSync.HasSyncMismatch Then

                mainStatusInfo.DisplayServerError = True

                Dim syncMismatchWarningDlg As New dlgServerSyncIssue
                With syncMismatchWarningDlg
                    .Owner = Me
                    .ShowDialog()
                    RestoreFromServer()  'restarts app when done
                End With

                Exit Sub

            End If

            If Not _isRestoring Then

                DBContext.ServerSynchronization = New ServerSync(DBContext, ServerDBContext)
                mainStatusInfo.DisplayServerError = False

                If ServerSync.DatabaseGUID <> "" Then

                    '- standard case: sync all pending items at startup
                    WPFToolbox.WaitForPriority(Threading.DispatcherPriority.Background)
                    DBContext.ServerSynchronization.SynchronizeAsync()

                Else

                    '- connecting existing non-demo database for the first time
                    FirstTimeConnect()

                End If

            Else

                _isRestoring = False

            End If

        Else

            'e.g. server unavailable
            Protocol_ConnectedChanged(False)

        End If

    End Sub


    ''' <summary>
    ''' Performs all data binding between data model and UI.
    ''' </summary>
    ''' 
    Private Sub ApplyAllDataBindings()

        '-- Assign initial contexts

        Me.DataContext = DBContext.tblUsers.First   'currently only one local user assumed

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


    ''' <summary>
    ''' Asynchronously checks for new version updates and displays the update notification in 
    ''' the application toolbar accordingly.
    ''' </summary>
    ''' 
    Private Async Sub CheckForUpdatesAsync()

        'IMPORTANT: It may take a few minutes until updates to the publisher version database 
        'actually become available to the PHP service.

        Dim newVersionStr = Await PhpServices.GetLatestAppVersionAsync
        If newVersionStr = "" Then
            Exit Sub 'server error
        End If

        Dim latestVersion = New Version(newVersionStr)
        If ApplicationVersion < latestVersion Then
            pnlStatus.ShowAvailableUpdate(newVersionStr)
        End If

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
                If .Count > 0 AndAlso .Item(0).DisplayIndex = -2 AndAlso newExpItem.DisplayIndex > -2 Then
                    .Insert(1, newExpItem) 'insert local experiment after server experiment
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
    ''' Processes for connecting a local database to the server for the first time. Checks for
    ''' userID duplicates and bulk uploads data . 
    ''' </summary>
    ''' 
    Private Sub FirstTimeConnect()

        '- check for duplicate server usernames -> will immediately restart app if conflicts were resolved by re-assigning UserId 

        If AreSameUserDataOnServer(DBContext.tblUsers.First) Then

            '- experiment data of the same user already are on the server -> restore or inform about rename
            If DBContext.tblUsers.First.tblExperiments.Count = 1 Then

                'one experiment present: usually after creating a conflicting new user, no need to offer choices here.
                MsgBox("The server already seems to contain your" + vbCrLf +
                       "experiment data (same userID and full name). " + vbCrLf +
                       "Please restore them from the server next ...", MsgBoxStyle.Information, "Restore Info")
                RestoreFromServer()
                Exit Sub

            Else

                'Multiple local experiments present: offer choice between restore and rename strategy
                Dim serverConflictDlg As New dlgServerConflict
                With serverConflictDlg
                    .Owner = Me
                    .ShowDialog()
                    If .UseRestoreStrategy Then
                        RestoreFromServer()
                    End If
                End With

            End If

        End If

        If ResolveDuplicateUsernames(DBContext, ServerDBContext) Then

            '- no userID conflicts with server

            mainStatusInfo.DisplayServerError = False

            ServerSync.DatabaseGUID = DBContext.tblDatabaseInfo.First.GUID

            Dim uploadDlg As New dlgUploadProgress(DBContext, ServerDBContext)
            With uploadDlg
                .Owner = Me
                .ShowDialog()
            End With
            DBContext.ServerSynchronization.SynchronizeAsync()

        End If

    End Sub



    ''' <summary>
    ''' Determines if the userID of the user attempting to connect already exists on the server, 
    ''' resulting in a duplicate. Displays a dialog with further actions, if found. 
    ''' </summary>
    ''' 
    Public Function ResolveDuplicateUsernames(localContext As ElnDbContext, serverContext As ElnDbContext) As Boolean

        Try

            'note: currently only one local user is supported in the UI
            Dim localUsers = (From user In localContext.tblUsers Select user.UserID).ToList

            Dim isFirst As Boolean = True
            Dim localDbGUID = localContext.tblDatabaseInfo.First.GUID
            Dim duplicateUsers = (From user In serverContext.tblUsers Where localUsers.Contains(user.UserID) AndAlso user.DatabaseID <> localDbGUID).ToList

            If duplicateUsers.Count > 0 Then

                For Each dupUser In duplicateUsers

                    If Not isFirst Then
                        MsgBox("There's another user-ID conflict -->", MsgBoxStyle.Information, "Duplicate")
                    End If

                    Dim duplicateDlg As New dlgChangeUsername(duplicateUsers, dupUser, localDbGUID)
                    With duplicateDlg
                        .Owner = Me
                        If .ShowDialog Then
                            If Not RenameUserID.DoRename(localContext, dupUser.UserID, .txtUsername.Text.ToLower) Then
                                Return False
                            End If
                        Else
                            Return True 'cancelled -> conflict still present
                        End If
                    End With
                    isFirst = False

                Next

                MsgBox("User-ID reassignments complete. The application " + vbCrLf +
                    "will restart now and upload your data.", MsgBoxStyle.Information, "Duplicate Resolution")

                CustomControls.My.MySettings.Default.Save()
                Process.Start(Environment.ProcessPath())
                Process.GetCurrentProcess().Kill()
                Return True 'dummy

            Else
                Return True
            End If
        Catch ex As Exception
            MsgBox(ex.Message + vbCrLf + ex.InnerException.Message)
            Return True
        End Try
    End Function


    ''' <summary>
    ''' Gets if a user entry with identical with the specified local user entry by userID, first name 
    ''' and last name already exists on the server. 
    ''' </summary>
    ''' <remarks>
    ''' Typically used to detect if a user should restore from the server instead 
    ''' of creating a new identical user from the demo, e.g. after a machine migration.
    ''' </remarks>
    ''' 
    Private Function AreSameUserDataOnServer(localUser As tblUsers) As Boolean

        If ServerDBContext IsNot Nothing Then
            Dim serverUser = (From user In ServerDBContext.tblUsers Where user.UserID = localUser.UserID).FirstOrDefault
            If serverUser IsNot Nothing Then
                With serverUser
                    Return localUser.FirstName.Equals(.FirstName, StringComparison.CurrentCultureIgnoreCase) AndAlso
                        localUser.LastName.Equals(.LastName, StringComparison.CurrentCultureIgnoreCase)
                End With
            End If
        End If

        Return False

    End Function


    Private Sub RestoreFromServer()

        If Not ServerSync.IsSynchronizing Then

            _isRestoring = True

            'display connect dialog if not connected (e.g. demo user)
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
                            MsgBox("The application now will restart with " + vbCrLf +
                              "the restored database.", MsgBoxStyle.Information, "Restore from Server")
                            CustomControls.My.MySettings.Default.RestoreFromServer = True
                            Me.Close()
                        End If
                    End With
                End If
            End With

            _isRestoring = False

        Else
            MsgBox("Retry in a moment, a server sync Is currently ongoing!", MsgBoxStyle.Information, "Restore from Server")
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

            MsgBox("The ELN server is unavailable!" + vbCrLf + "Changes currently are not backed up." + vbCrLf + vbCrLf +
                   "Try to reconnect later ...", MsgBoxStyle.Information, "Server Sync")

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
    ''' Handles info panel request to create a first non-demo user-
    ''' </summary>
    ''' 
    Private Sub DemoStatus_RequestCreateFirstUser(sender As Object)

        CreateFirstUser()

    End Sub


    ''' <summary>
    ''' Creates the first non-demo user
    ''' </summary>
    ''' 
    Private Sub CreateFirstUser()

        If CType(Me.DataContext, tblUsers).UserID = "demo" Then

            If Users.CreateFirstUser(Me) Then

                'Connect data model of local database with UI
                ApplyAllDataBindings()

                Dim currUser = CType(Me.DataContext, tblUsers)
                expNavTree.SelectExperiment(currUser.tblExperiments.First)

                Dim res2 = MsgBox("Done! - If the optional Phoenix ELN server " + vbCrLf +
                                  "database is installed in your environment," + vbCrLf +
                                  "would you like to connect to it now for  " + vbCrLf +
                                  "backup and in-house data sharing?",
                                  MsgBoxStyle.Information + MsgBoxStyle.YesNo, "Server Connection")
                If res2 = MsgBoxResult.Yes Then
                    ConnectToServer()
                End If

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
                        Case Key.D2
                            .AddReagent()
                        Case Key.D3
                            .AddSolvent()
                        Case Key.D4
                            .AddAuxiliary()
                        Case Key.D5
                            .AddProduct()
                        Case Key.D6
                            .AddComment()
                        Case Key.D7
                            .AddSeparator()
                        Case Key.D8
                            .AddFiles()
                    End Select
                End With

                'allow text edits to undo/redo before higher-level undo/redo occurs

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

        Dim result = True

        Dim currTreeEditItem = WPFToolbox.FindVisualParent(Of TreeViewEditLabel)(Mouse.DirectlyOver)
        If (TreeViewEditLabel.CurrentEditItem IsNot Nothing) AndAlso (TreeViewEditLabel.CurrentEditItem IsNot currTreeEditItem) Then
            If Not TreeViewEditLabel.CurrentEditItem.EndEdit Then 'also performs validation
                result = False
            End If
        End If

        Dim currListEditItem = WPFToolbox.FindVisualParent(Of ListBoxEditLabel)(Mouse.DirectlyOver)
        If (ListBoxEditLabel.CurrentEditItem IsNot Nothing) AndAlso (ListBoxEditLabel.CurrentEditItem IsNot currListEditItem) Then
            If Not ListBoxEditLabel.CurrentEditItem.EndEdit Then 'also performs validation
                result = False
            End If
        End If

        Return result

    End Function


    Private Sub Me_PreviewMouseDown(sender As Object, e As RoutedEventArgs) Handles Me.PreviewMouseDown

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

        'final save
        DBContext.SaveChanges()

        'restart ELN if restoring from server
        If CustomControls.My.MySettings.Default.RestoreFromServer = True Then
            Process.Start(Environment.ProcessPath())
            Process.GetCurrentProcess().Kill()
        End If

    End Sub


    Private Sub Warning_Delegate()

        MsgBox("The demo experiments limit is reached." + vbCrLf + vbCrLf +
               "Click the green 'Create User' button" + vbCrLf +
               "to start with a unique user now.", MsgBoxStyle.Information, "Demo Validation")

    End Sub


    ''' <summary>
    ''' Add or clone a new experiment
    ''' </summary>
    '''
    Private Sub expNavTree_RequestAddExperiment(sender As Object, projectEntry As tblProjects) Handles expNavTree.RequestAddExperiment

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

        If SelectedExpContent.CreateExperiment(DBContext, expNavTree, projectEntry) Then

            pnlInfo.DataContext = Nothing
            pnlInfo.DataContext = CType(Me.DataContext, tblUsers)

        End If

    End Sub


    ''' <summary>
    ''' Handles experiment selection within RSS results and step summary control.
    ''' </summary>
    ''' 
    Private Sub ExpList_RequestOpenExperiment(sender As Object, targetExp As tblExperiments, isFromServer As Boolean)

        If Not isFromServer Then

            '-- local experiment

            expNavTree.SelectExperiment(targetExp)

        Else

            '-- server experiment

            If tabExperiments.Items.Count > 4 AndAlso Not ExpDisplayList.First.DisplayIndex = -2 Then

                'warn for too many open exp tabs
                Dim res = MsgBox("The maximum of 5 open experiments will be" + vbCrLf +
                                 "exceeded if opening the server experiment." + vbCrLf + vbCrLf +
                                 "Release the rightmost experiment?", MsgBoxStyle.OkCancel + MsgBoxStyle.Information, "Pin Limit")
                If res = MsgBoxResult.Ok Then
                    Dim lastIndex = tabExperiments.Items.Count - 1
                    CType(tabExperiments.Items(lastIndex), tblExperiments).DisplayIndex = Nothing
                Else
                    Exit Sub
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

        End If

    End Sub


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
                thisTab.IsSelected = True

            Else

                '- experiment not yet present in tab

                Dim leftmostExp = CType(tabExperiments.Items(0), tblExperiments)
                Dim localStartIndex = If(leftmostExp.DisplayIndex = -2, 1, 0)   'displayIndex = -2 means server experiment (leftmost)
                Dim firstLocalExp = CType(tabExperiments.Items(localStartIndex), tblExperiments)

                If firstLocalExp IsNot Nothing Then

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

                    UpdateExperimentTabs(selExp)
                    DBContext.SaveChanges()     'no exp-level undo/redo required

                End If

                tabExperiments.SelectedIndex = localStartIndex 'select first tab

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

            Case -1

                'undo pre-pin of leftmost local experiment
                targetExp.DisplayIndex = 0

            Case 0

                If tabExperiments.Items.Count < 5 Then

                    'pre-pin leftmost local experiment
                    targetExp.DisplayIndex = -1

                Else

                    MsgBox("Can't pin this experiment, since the maximum " + vbCrLf +
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


    Private Sub btnAddProject_Click() Handles btnAddProject.Click
        expNavTree.AddProject()
    End Sub


    Private Sub btnPrint_Click() Handles btnPrint.Click

        ExperimentPrint.Print(SelectedExpContent, False, Me)
    End Sub


    Private Sub btnPDF_Click() Handles btnPDF.Click

        ExperimentPrint.Print(SelectedExpContent, True, Me)

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
                ExperimentBase.ExportExperiment(currExp, .FileName, ApplicationVersion.ToString)
                CustomControls.My.MySettings.Default.LastExportDir = IO.Path.GetDirectoryName(.FileName)
            End If

        End With

    End Sub


    Private Sub btnEditUser_Click() Handles btnEditUser.Click

        Dim newUserDlg As New dlgNewUser(DBContext, ServerDBContext)

        With newUserDlg
            .Owner = Me
            .CurrentUser = CType(Me.DataContext, tblUsers)  'specifying a user means to edit its data
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


    Private Sub mnuHelp_Click() Handles mnuHelp.Click

        Dim info As New ProcessStartInfo("https://abrechts.github.io/phoenix-eln-help.github.io/pages/1CreateExperiment.html")
        info.UseShellExecute = True
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
            MsgBox("Sorry, can 't access version server.", MsgBoxStyle.Information, "Update Check")
            Exit Sub 'server error
        End If

        Dim latestVersion = New Version(newVersionStr)
        If ApplicationVersion < latestVersion Then
            pnlStatus.ShowAvailableUpdate(newVersionStr)
            MsgBox("An update is available! See the" + vbCrLf +
                   "info panel for details ...", MsgBoxStyle.Information, "Update Check")
        Else
            MsgBox("Your application is up-to-date!", MsgBoxStyle.Information, "Update Check")
        End If

    End Sub


    Private Sub btnSearch_Click() Handles btnSearch.Click

        Dim searchDlg As New dlgSearch
        With searchDlg
            .Owner = Me
            .LocalDBContext = DBContext
            .ServerDBContext = ServerDBContext
            With CustomControls.My.MySettings.Default
                dlgSearch.IsServerQuery = .IsServerQuery
                searchDlg.ShowDialog()
                .IsServerQuery = dlgSearch.IsServerQuery
            End With
        End With

    End Sub


    Private Sub btnSequences_Click() Handles btnSequences.Click

        Dim refExp = CType(SelectedExpContent.DataContext, tblExperiments)

        If refExp IsNot Nothing Then
            Dim seqDlg As New dlgSequences()
            With seqDlg
                .BuildSequences(refExp, DBContext)
                .ShowDialog()
            End With
        End If

    End Sub


    Private Sub btnExpInfo_Click() Handles btnExpInfo.Click

        Dim expInfoDlg As New dlgExperimentInfo
        With expInfoDlg
            .Owner = Me
            .DataContext = tabExperiments.SelectedContent
            .ShowDialog()
        End With

    End Sub




    ''' <summary>
    ''' Displays server connect dialog and return true if the connection succeeded
    ''' </summary>
    ''' 
    Friend Function ConnectToServer(Optional isRestore As Boolean = False) As Boolean

        If CType(Me.DataContext, tblUsers).UserID = "demo" AndAlso Not isRestore Then

            MsgBox("Sorry, the 'demo' user can't connect " + vbCrLf +
                   "to the server database.", MsgBoxStyle.Information, "Server Connect")
            Return False

        End If

        Dim connDlg As New dlgServerConnection(DBContext, ServerDBContext)

        With connDlg
            .Owner = Me

            If .ShowDialog() Then

                If .NewServerContext IsNot Nothing Then

                    '-- apply new server context
                    ServerDBContext = .NewServerContext
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

