Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports System.IO
Imports System.Windows.Input
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading

Public Class FileContent

    Public Shared Event MouseOverChanged(sender As Object, isEntering As Boolean)

    Private WithEvents _fileWatcher As FileSystemWatcher
    Private Shared WithEvents _cleanupTimer As New DispatcherTimer


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets or gets a list of all currently active FileSystemWatchers.
    ''' </summary>
    ''' 
    Public Shared Property ActiveFileWatchers As New List(Of FileSystemWatcher)


    ''' <summary>
    ''' Sets or gets the file path of the temporary copy of the embedded document.
    ''' </summary>
    ''' 
    Private Property TempFilePath As String


    Private Sub iconImage_MouseEnter() Handles iconImage.MouseEnter
        RaiseEvent MouseOverChanged(Me, True)
    End Sub


    Private Sub iconImageImage_MouseLeave() Handles iconImage.MouseLeave
        RaiseEvent MouseOverChanged(Me, False)
    End Sub


    Private Sub iconImage_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles iconImage.MouseLeftButtonUp

        OpenFile(Me.DataContext)

    End Sub


    Private Sub txtFileTitle_GotKeyboardFocus() Handles txtFileTitle.GotKeyboardFocus

        txtFileTitle.Select(255, 0)

    End Sub

    ''' <summary>
    ''' Enters document title editing.
    ''' </summary>
    ''' 
    Public Sub ActivateEdit()

        txtFileTitle.Focus()
        txtFileTitle.Select(255, 0)

    End Sub


    Private Sub txtFileTitle_LostFocus() Handles Me.LostFocus

        With txtFileTitle
            If .CanUndo Then
                .IsUndoEnabled = False  'clears the undo stack
                .IsUndoEnabled = True   'restarts it again
                Dim myProtocol = WPFToolbox.FindVisualParent(Of Protocol)(Me)
                myProtocol?.AutoSave()
            End If
        End With

    End Sub



    ''' <summary>
    ''' Gets the path to the directory in the user's temp folder, in which temporary 
    ''' embedded docs created for viewing are stored.   
    ''' </summary>
    '''
    Public Shared Function TempDocsPath() As String

        If _tempDocsPath = "" Then
            _tempDocsPath = Path.GetTempPath + "PhoenixElnTempDocs"
            If Not Directory.Exists(_tempDocsPath) Then
                Directory.CreateDirectory(_tempDocsPath)
            End If
        End If
        Return _tempDocsPath

    End Function

    Private Shared _tempDocsPath As String


    ''' <summary>
    ''' Creates a tmp file from the DB byte array and opens it in its default application.
    ''' </summary>
    ''' 
    Friend Sub OpenFile(embeddedFilesEntry As tblEmbeddedFiles)

        With embeddedFilesEntry

            Dim fileName = .FileName
            fileName = String.Join("_", fileName.Split(Path.GetInvalidFileNameChars()))   'remove invalid chars
            TempFilePath = TempDocsPath() + "\" + fileName

            Try
                'deserialize file to temp folder
                File.WriteAllBytes(TempFilePath, .FileBytes)

            Catch ex1 As Exception

                MsgBox("Can't open this document, since it's already " + vbCrLf +
                       "opened by another application.", MsgBoxStyle.Information, "Embedded Document")
                Exit Sub

            End Try

            'Unfortunately, the use of processes is chaotic for Office docs; when e.g. opening a second Excel file,
            'its initial process is immediately killed. Therefore the process.exit event can't be utilized reliably. 

            Using docProcess = New Process
                With docProcess
                    .StartInfo.FileName = TempFilePath
                    .StartInfo.UseShellExecute = True
                    .Start()
                End With
            End Using

            _fileWatcher = New FileSystemWatcher
            With _fileWatcher
                .Path = TempDocsPath()
                .Filter = fileName
                .EnableRaisingEvents = True
            End With

            ActiveFileWatchers.Add(_fileWatcher)

            End With

    End Sub


    ''' <summary>
    ''' Serializes the specified file to a byte array for DB storage. Does nothing if the 
    ''' parent experiment is finalized.
    ''' </summary>
    ''' <param name="filePath">Path to the file to serialize.</param>
    ''' 
    Friend Sub SerializeFile(ByVal filePath As String)

        Dim embeddedEntry = CType(Me.DataContext, tblEmbeddedFiles)
        Dim isExpFinalized = embeddedEntry.ProtocolItem.Experiment.WorkflowState = WorkflowStatus.Finalized

        If Not isExpFinalized Then

            'don't update finalized experiment

            With embeddedEntry
                Try

                    .FileBytes = File.ReadAllBytes(filePath)

                Catch ex As IO.IOException

                    'Workaround: For unknown reasons ReadAllBytes does not allow to serialize locked
                    'files (is read only). Since file copy works fine in these cases, the file is 
                    'copied to a temp file before serializing ... 

                    Dim tmpPath = TempDocsPath() + "\copy.tmp"
                    File.Copy(filePath, tmpPath, True)
                    .FileBytes = File.ReadAllBytes(tmpPath)
                    File.Delete(tmpPath)

                End Try
            End With

        End If

    End Sub


    ''' <summary>
    ''' Gets the file icon associated with the file appendix of the specified filePath 
    ''' as BitmapImage byte stream.
    ''' </summary>
    '''
    Public Shared Function GetFileIconBytes(filePath) As Byte()

        Dim iconImage = IconExtract.BitmapImageFromFile(filePath, IconExtract.IconFlags.iconLarge)
        Dim buffer As Byte() = Nothing

        If iconImage IsNot Nothing Then

            Dim imageSource = DirectCast(iconImage, BitmapImage)
            Dim encoder As New PngBitmapEncoder()
            Dim bf As BitmapFrame = BitmapFrame.Create(imageSource)
            bf.Freeze()
            encoder.Frames.Add(bf)

            Using memstream As New System.IO.MemoryStream()
                encoder.Save(memstream)
                buffer = memstream.GetBuffer()
            End Using

        End If

        Return buffer

    End Function


    ''' <summary>
    ''' Saves the specified embedded file to a selected file location.
    ''' </summary>
    '''
    Friend Sub ExportFile(embeddedFilesEntry As tblEmbeddedFiles)

        Dim saveDlg = New Microsoft.Win32.SaveFileDialog()
        With saveDlg
            .FileName = embeddedFilesEntry.FileName
            .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            If .ShowDialog = True Then
                Try
                    File.Copy(TempDocsPath, .FileName)
                Catch ex3 As Exception
                    MsgBox(ex3.Message, MsgBoxStyle.Exclamation, "Document Export")
                End Try
            End If
        End With

    End Sub


    ''' <summary>
    ''' Track save operations for current file in temp folder
    ''' </summary>
    ''' <remarks>Often, but not always fires the Changed event, but also the Renamed event, e.g. for Office 
    ''' docs renaming a tmp file to the final one.</remarks>

    Private Sub _FileWatcher_Change(sender As Object, e As FileSystemEventArgs) Handles _fileWatcher.Changed

        Dispatcher.BeginInvoke(DispatcherPriority.Background, New Action(Sub() SerializeFile(TempFilePath)))

    End Sub

    Private Sub _FileWatcher_Rename(sender As Object, e As RenamedEventArgs) Handles _fileWatcher.Renamed

        Dispatcher.BeginInvoke(DispatcherPriority.Background, New Action(Sub() SerializeFile(TempFilePath)))

    End Sub

    Private Sub _FileWatcher_Created(sender As Object, e As FileSystemEventArgs) Handles _fileWatcher.Created

        Dispatcher.BeginInvoke(DispatcherPriority.Background, New Action(Sub() SerializeFile(TempFilePath)))

    End Sub


    ''' <summary>
    ''' Periodically removes the pending documents of the experiment from the temp folder, if not locked, and  
    ''' releases their FileSystemWatchers. See <see cref="CleanUpInactiveEditSessions"/>
    ''' </summary>
    ''' 
    Public Shared Sub StartDocEditCleanupTimer(interval As TimeSpan)

        With _cleanupTimer
            .Interval = interval
            .IsEnabled = True
            .Start()
        End With

    End Sub

    Private Shared Sub _CleanupTimer_Tick() Handles _cleanupTimer.Tick

        CleanUpInactiveEditSessions()

    End Sub


    ''' <summary>
    ''' Removes the pending documents of the experiment from the temp folder, if not locked, and  
    ''' releases their FileSystemWatchers.
    ''' </summary>
    ''' <remarks>
    ''' This procedure is mainly important for users who don't frequently close their 
    ''' apps at the end of the day. The undisposed file system watchers and temp files 
    ''' would accumulate over time in such scenarios.
    ''' </remarks>
    ''' 
    Private Shared Sub CleanUpInactiveEditSessions()

        For i = ActiveFileWatchers.Count - 1 To 0 Step -1

            Dim watcher = ActiveFileWatchers(i)
            Dim watchedFile = watcher.Path + "\" + watcher.Filter

            Try
                'if delete is successful, the file is unlocked
                watcher.EnableRaisingEvents = False
                File.Delete(watchedFile)

                'file isn't locked (anymore), the watcher can be disposed
                watcher.Dispose()
                ActiveFileWatchers.Remove(watcher)

            Catch ex As Exception

                'do nothing if file locked
                watcher.EnableRaisingEvents = True

            End Try

        Next

    End Sub


    ''' <summary>
    ''' Deletes all temporary embedded files from the temporary document folder, if not locked.
    ''' </summary>
    ''' 
    Public Shared Sub CleanupTempFolderDocs()

        Dim dirInfo As New DirectoryInfo(TempDocsPath)

        For Each tmpFile In dirInfo.GetFiles()
            Try
                tmpFile.Delete()
            Catch ex As Exception
                'do nothing if file locked by another application
            End Try
        Next

    End Sub


    ''' <summary>
    ''' Gets a list of all file extensions which for security reasons cannot be opened by 
    ''' Adobe Acrobat viewer if embedded in a PDF as attachment.
    ''' </summary>
    ''' 
    Public Shared ReadOnly Property PdfFileTypeExclusions As List(Of String)
        Get
            Dim exclList As New List(Of String)
            exclList.AddRange(New String() {
                ".ade", ".adp", ".app", ".arc", ".arj", ".asp", ".bas", ".bat", ".bz", ".bz2", ".cab",
                ".chm", ".class", ".cmd", ".com", ".command", ".cpl", ".crt", ".csh", ".desktop", ".dll",
                ".exe", ".fxp", ".gz", ".hex", ".hlp", ".hqx", ".hta", ".inf", ".ini", ".ins", ".isp",
                ".its", ".job", ".js", ".jse", ".ksh", ".lnk", ".lzh", ".mad", ".maf", ".mag", ".mam",
                ".maq", ".mar", ".mas", ".mat", ".mau", ".mav", ".maw", ".mda", ".mdb", ".mde", ".mdt",
                ".mdw", ".mdz", ".msc", ".msi", ".msp", ".mst", ".ocx", ".ops", ".pcd", ".pi", ".pif",
                ".prf", ".prg", ".pst", ".rar", ".reg", ".scf", ".scr", ".sct", ".sea", ".shb", ".shs",
                ".sit", ".tar", ".taz", ".tgz", ".tmp", ".url", ".vb", ".vbe", ".vbs", ".vsmacros",
                ".vss", ".vst", ".vsw", ".webloc", ".ws", ".wsc", ".wsf", ".wsh", ".z", ".zip", ".zlo",
                ".zoo", ".fdf", ".jar", ".pkg", ".tool", ".term", ".acm", ".asa", ".aspx", ".ax", ".ad",
                ".application", ".asx", ".cer", ".cfg", ".chi", ".class", ".clb", ".cnt", ".cnv", ".cpx",
                ".crx", ".der", ".drv", ".fon", ".gadget", ".grp", ".htt", ".ime", ".jnlp", ".local",
                ".manifest", ".mmc", ".mof", ".msh", ".msh1", ".msh2", ".mshxml", ".msh1xml", ".msh2xml",
                ".mui", ".nls", ".pl", ".perl", ".plg", ".ps1", ".ps2", ".ps1xml", ".ps2xml", ".psc1",
                ".psc2", ".py", ".pyc", ".pyo", ".pyd", ".rb", ".sys", ".tlb", ".tsp", ".xbap", ".xnk",
                ".xpi", ".air", ".appref-ms", ".desklink", ".glk", ".library-ms", ".mapimail", ".mydocs",
                ".sct", ".search-ms", ".searchConnector-ms", ".vxd", ".website", ".zfsendtotarget"
            })
            Return exclList
        End Get
    End Property


End Class
