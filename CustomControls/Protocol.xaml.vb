Imports System.ComponentModel
Imports System.Globalization
Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Media
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports GongSolutions.Wpf.DragDrop
Imports Microsoft.Win32

Public Class Protocol

    Public Event RequestUpdateSketchComponentLabels(sender As Object)

    Public Shared Event WorkflowStateChanged(sender As Object)
    Public Shared Event RequestSaveIcon(sender As Object)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        ProtocolItemsViewSrc = Me.TryFindResource("ProtocolItemsView")
        addToolbar.ParentProtocol = Me

        Try
            Dim mat = PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformToDevice
            Dim dpiFactor = mat.M11     '1 = standard 96 DPI; 2 = 192 dpi (4k)

            If dpiFactor < 2 Then
                Using lockMs As New MemoryStream(My.Resources.curs_lock)
                    LockCursor = New Cursor(lockMs)
                End Using
            Else
                Using lockMs As New MemoryStream(My.Resources.curs_lock_large)
                    LockCursor = New Cursor(lockMs)
                End Using
            End If

        Catch
            ' improves XAML editor stability
        End Try

        AddHandler ProductContent.RequestEditProductPlaceholder, AddressOf ProductContent_RequestEditProductPlaceholder
        AddHandler ImageContent.MouseOverChanged, AddressOf AccessibleContent_MouseOverChanged
        AddHandler FileContent.MouseOverChanged, AddressOf AccessibleContent_MouseOverChanged
        AddHandler ExpTreeHeader.RequestUpdateWorkflowState, AddressOf ExpTreeHeader_RequestUpdateWorkflowState
        AddHandler ServerSync.SyncComplete, AddressOf ServerSync_SyncComplete
        AddHandler ProtocolItemBase.ElementLeftMouseUp, AddressOf ProtocolItemBase_ElementMouseUp

    End Sub


    ''' <summary>
    ''' Sets or gets the lock cursor displayed over the finalized experiment content.
    ''' </summary>
    ''' 
    Friend Shared Property LockCursor As Cursor


    ''' <summary>
    ''' CollectionViewSource for establishing ProtocolItem sorting by sequenceNr.
    ''' </summary>
    '''
    Private Property ProtocolItemsViewSrc As CollectionViewSource


    ''' <summary>
    ''' Sets or gets information about the associated reaction sketch.
    ''' </summary>
    ''' 
    Public Property SketchInfo As SketchResults


    ''' <summary>
    ''' Gets the Undo/Redo tracker for the current experiment.
    ''' </summary>
    ''' 
    Public Property UndoEngine As UndoRedo


    Private _wasSkipped As Boolean = False

    ''' <summary>
    ''' Performs a save operation after specific key operations, creates an Undo entry for the  
    ''' UndoRedo engine, and synchronized to the server in the background, if present and active.
    ''' </summary>
    ''' 
    ''' <param name="allowFinalized">Set to true for special cases where also finalized experiments are to be 
    ''' processed. Typically used for save & sync of modified workflow states. Default is false.</param>
    ''' <param name="noUndoPoint">Set to true for special cases where no Undo point should be created, 
    ''' e.g. for auto-saving an Undo/Redo operation, or after creating a new experiment. The default is false.</param>
    ''' 
    Public Sub AutoSave(Optional allowFinalized As Boolean = False, Optional noUndoPoint As Boolean = False)

        Dim expEntry = CType(Me.DataContext, tblExperiments)
        If allowFinalized OrElse expEntry.WorkflowState <> WorkflowStatus.Finalized Then

            '- create Undo point
            If Not noUndoPoint Then
                UndoEngine.CreateUndoPoint(lstProtocol.SelectedItem)
            End If

            With ExperimentContent.DbContext

                '- save locally, which also sets sync info flags and tombstone
                .SaveChanges()

                If ServerSync.IsConnected Then

                    If .ServerSynchronization IsNot Nothing Then
                        If Not ServerSync.IsSynchronizing Then
                            '- sync to server asynchronously
                            .ServerSynchronization.SynchronizeAsync()
                        Else
                            '- skip this sync while another one is ongoing
                            _wasSkipped = True
                            '  Debug.WriteLine("skipped: " + Now.ToString)
                        End If
                    Else
                        'try to reconnect with no startup connection (serverSync is nothing)
                        With My.Settings
                            ServerSync.CreateServerContextAsync(.ServerName, .ServerDbUserName, .ServerDbPassword, .ServerPort,
                              ExperimentContent.DbContext.tblDatabaseInfo.First)
                        End With
                    End If

                End If

                RaiseEvent RequestSaveIcon(Me)

            End With

        End If

    End Sub


    Private Sub ServerSync_SyncComplete()

        With ExperimentContent.DbContext.ServerSynchronization

            'Sync again after completion, if a sync request was skipped due to
            'a request while the now completed sync was ongoing.

            If _wasSkipped Then
                If ServerSync.IsConnected Then
                    'Debug.WriteLine("-- Retry Skipped ---")
                    .SynchronizeAsync()
                End If
                _wasSkipped = False
            End If
        End With

    End Sub


    ''' <summary>
    ''' Performs an Undo operation
    ''' </summary>
    ''' 
    Public Sub Undo()

        Dim selectedItem = UndoEngine.Undo()

        Dim expEntry = CType(Me.DataContext, tblExperiments)
        If expEntry.tblProtocolItems.Contains(selectedItem) Then
            lstProtocol.SelectedItem = selectedItem
        Else
            lstProtocol.UnselectAll()
        End If

        AutoSave(, noUndoPoint:=True)

    End Sub


    ''' <summary>
    ''' Performs a Redo operation
    ''' </summary>
    ''' 
    Public Sub Redo()

        Dim selectedItem = UndoEngine.Redo()

        Dim expEntry = CType(Me.DataContext, tblExperiments)
        If expEntry.tblProtocolItems.Contains(selectedItem) Then
            lstProtocol.SelectedItem = selectedItem
        Else
            lstProtocol.UnselectAll()
        End If

        AutoSave(, noUndoPoint:=True)

    End Sub


    ''' <summary>
    ''' Sets the UI element properties to a state suited for printing.
    ''' </summary>
    ''' 
    Public Sub SetPrintUI()

        IsPrinting = True
        addToolbar.Visibility = Visibility.Collapsed
        pnlProtocol.Background = Brushes.Transparent
        pnlDock.Margin = New Thickness(0)
        scrlProtocol.Margin = New Thickness(4, 4, 4, 8)

        UnselectAll()

    End Sub


    ''' <summary>
    ''' Gets if the protocol is in print mode.
    ''' </summary>
    ''' 
    Public Property IsPrinting As Boolean
        Get
            Return _IsPrinting
        End Get
        Private Set(value As Boolean)
            _IsPrinting = value
        End Set
    End Property

    Private _IsPrinting As Boolean = False


    ''' <summary>
    ''' Unselects all selected protocol items.
    ''' </summary>
    ''' 
    Public Sub UnselectAll()

        Dim expEntry = CType(Me.DataContext, tblExperiments)
        For Each item In expEntry.tblProtocolItems
            item.IsSelected = 0
        Next

        lstProtocol.UnselectAll()

    End Sub


    ''' <summary>
    ''' Reflects applied changes to the experiment workflow state in the protocol UI (e.g if locked by finalizing).
    ''' </summary>
    ''' 
    Public Sub UpdateLockState()

        AutoSave(allowFinalized:=True)
        Me_DataContextChanged()

    End Sub


    ''' <summary>
    ''' Allows the use of the MouseWheel for scrolling the protocol
    ''' </summary>
    ''' 
    Private Sub lstProtocol_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles lstProtocol.PreviewMouseWheel

        e.Handled = True

        Dim e2 As New MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        e2.RoutedEvent = ListBox.MouseWheelEvent
        e2.Source = e.Source

        lstProtocol.RaiseEvent(e2)

    End Sub


    ''' <summary>
    ''' Adapt UI to the workflow state of the current experiment
    ''' </summary>
    ''' 
    Private Sub Me_DataContextChanged() Handles Me.DataContextChanged

        If TypeOf Me.DataContext Is tblExperiments Then

            Dim expEntry = CType(Me.DataContext, tblExperiments)

            If expEntry.WorkflowState = WorkflowStatus.Finalized Then

                Me.Cursor = LockCursor
                Me.ForceCursor = True
                lstProtocol.ItemContainerStyle = FindResource("ProtocolFinalizedListBoxItemStyle")
                lstProtocol.AllowDrop = False
                UnselectAll()
                If UndoEngine IsNot Nothing Then
                    UndoEngine.Reset()
                End If

            Else

                Me.Cursor = Cursors.Arrow
                Me.ForceCursor = False
                lstProtocol.ItemContainerStyle = FindResource("ProtocolListBoxItemStyle")
                lstProtocol.AllowDrop = True
                lstProtocol.Focus()

            End If

        End If

    End Sub


    ''' <summary>
    ''' sets or gets the vertical offset of the protocol scrollbar
    ''' </summary>
    ''' 
    Public Property VerticalScrollOffset As Double
        Get
            Return scrlProtocol.VerticalOffset
        End Get
        Set(value As Double)
            scrlProtocol.ScrollToVerticalOffset(value)
        End Set
    End Property


    Private Sub ProductContent_RequestEditProductPlaceholder(sender As Object, lbItem As ListBoxItem)

        Dim protocolItemEntry = CType(lbItem.Content, tblProtocolItems)

        Dim prodIndex? As Integer = Nothing
        If protocolItemEntry.TempInfo <> "" Then
            Dim info = protocolItemEntry.TempInfo.Split("/"c)
            If info.Length = 3 Then
                prodIndex = Val(info(1))
            End If
            protocolItemEntry.TempInfo = Nothing
        End If

        If AddProduct(prodIndex) Then

            protocolItemEntry.Experiment.tblProtocolItems.Remove(protocolItemEntry)
            UpdateElementSequenceNumbers()
            ProtocolItemsViewSrc.View.Refresh()

        End If

    End Sub


    Private Sub AccessibleContent_MouseOverChanged(sender As Object, isEntering As Boolean)

        If Not addToolbar.IsVisible Then
            If isEntering Then
                Me.Cursor = Cursors.Hand
            Else
                Me.Cursor = LockCursor
            End If
        End If

    End Sub


    Private Sub ExpTreeHeader_RequestUpdateWorkflowState(sender As Object, expEntry As tblExperiments, requestedState As WorkflowStatus)

        ChangeWorkflowState(expEntry, requestedState)

    End Sub


    Public Sub ChangeWorkflowState(expEntry As tblExperiments, requestedState As WorkflowStatus)

        If requestedState = WorkflowStatus.Finalized Then

            If expEntry.RxnSketch IsNot Nothing Then
                Dim finalizeDlg As New dlgFinalize(expEntry.User)
                With finalizeDlg
                    .Owner = WPFToolbox.FindVisualParent(Of Window)(Me)
                    If .ShowDialog Then
                        expEntry.FinalizeDate = Now.ToString("yyyy-MM-dd HH:mm")
                        expEntry.WorkflowState = WorkflowStatus.Finalized
                        UpdateLockState()   'includes auto-save
                    End If
                End With
            Else
                MsgBox("Cannot finalize an empty experiment!", MsgBoxStyle.Information, "Finalize Error")
            End If

        Else

            Dim reopenDlg As New dlgReopen(expEntry.User)
            With reopenDlg
                .Owner = WPFToolbox.FindVisualParent(Of Window)(Me)
                If .ShowDialog Then
                    expEntry.WorkflowState = WorkflowStatus.Unlocked
                    UpdateLockState()
                End If
            End With

        End If

        RaiseEvent WorkflowStateChanged(Me)

    End Sub


    ''' <summary>
    ''' Clear current protocol item selection when clicking into empty protocol area.
    ''' </summary>
    ''' 
    Private Sub lstProtocol_PreviewMouseDown(sender As Object, e As RoutedEventArgs) Handles lstProtocol.PreviewMouseDown

        If TypeOf e.OriginalSource Is ScrollViewer Then
            UnselectAll()
        End If

    End Sub


    ''' <summary>
    ''' Handles opening the target protocol item to initiate edit operations.
    ''' </summary>
    ''' 
    Private Sub ProtocolItemBase_ElementMouseUp(sender As Object, e As MouseEventArgs)

        'don't allow editing protocol elements of finalized experiments
        If CType(Me.DataContext, tblExperiments).WorkflowState = WorkflowStatus.Finalized Then
            e.Handled = True
            Exit Sub
        End If

        Dim protocolItemEntry = CType(sender.Content, tblProtocolItems)

        Select Case protocolItemEntry.ElementType

            Case ProtocolElementType.RefReactant

                Dim addReactDlg As New dlgEditRefReactant

                With addReactDlg
                    SetExpContentCenterStartupPos(addReactDlg)
                    .IsAddingNew = False
                    .SketchInfo = SketchInfo
                    .ReferenceReactant = protocolItemEntry.tblRefReactants
                    If .ShowDialog() Then
                        AlignRefReactantProperties(protocolItemEntry.tblRefReactants)
                        UpdateRefReactants()
                        RecalculateExperiment()
                        RaiseEvent RequestUpdateSketchComponentLabels(Me)
                        AutoSave()
                    End If
                End With

            Case ProtocolElementType.Reagent

                Dim addReagentDlg As New dlgEditReagent
                With addReagentDlg
                    SetExpContentCenterStartupPos(addReagentDlg)
                    .IsAddingNew = False
                    .ReagentEntry = protocolItemEntry.tblReagents
                    If .ShowDialog() Then
                        AutoSave()
                    End If
                End With

            Case ProtocolElementType.Solvent

                Dim addSolventDlg As New dlgEditSolvent
                With addSolventDlg
                    SetExpContentCenterStartupPos(addSolventDlg)
                    .IsAddingNew = False
                    .SolventEntry = protocolItemEntry.tblSolvents
                    If .ShowDialog() Then
                        AutoSave()
                    End If
                End With

            Case ProtocolElementType.Auxiliary

                Dim addAuxDlg As New dlgEditAuxiliary
                With addAuxDlg
                    SetExpContentCenterStartupPos(addAuxDlg)
                    .IsAddingNew = False
                    .AuxiliaryEntry = protocolItemEntry.tblAuxiliaries
                    If .ShowDialog() Then
                        AutoSave()
                    End If
                End With

            Case ProtocolElementType.Product

                If protocolItemEntry.tblProducts IsNot Nothing Then
                    Dim addProdDlg As New dlgEditProduct
                    With addProdDlg
                        SetExpContentCenterStartupPos(addProdDlg)
                        .IsAddingNew = False
                        .SketchInfo = SketchInfo
                        .ProductEntry = protocolItemEntry.tblProducts
                        If .ShowDialog() Then
                            RecalculateExperiment(skipRecalcDialog:=True)
                            RaiseEvent RequestUpdateSketchComponentLabels(Me)
                            AutoSave()
                        End If
                    End With
                Else
                    'handle cloning product placeholder
                    If TypeOf sender Is ListBoxItem Then
                        Dim verticalScrollPos = scrlProtocol.VerticalOffset
                        ProductContent_RequestEditProductPlaceholder(Me, sender)    'contains auto-save
                        scrlProtocol.ScrollToVerticalOffset(verticalScrollPos)
                    End If
                End If

            Case ProtocolElementType.Comment

                'do nothing, no dialog required

        End Select

    End Sub


    Private Sub SetExpContentCenterStartupPos(targetWindow As Window)

        Dim expContent = WPFToolbox.FindVisualParent(Of ExperimentContent)(Me)

        Dim screenPos = expContent.PointToScreen(New Windows.Point(0, 0))
        Dim middleX = screenPos.X + (expContent.ActualWidth - targetWindow.ActualWidth) / 2
        Dim middleY = screenPos.Y + (expContent.ActualHeight - targetWindow.ActualHeight) / 2

        targetWindow.WindowStartupLocation = WindowStartupLocation.Manual
        Dim dpi = VisualTreeHelper.GetDpi(Me)
        targetWindow.Left = middleX / dpi.DpiScaleX
        targetWindow.Top = (middleY + 60) / dpi.DpiScaleY

    End Sub


    ''' <summary>
    ''' Add a new reference reactant
    ''' </summary>
    ''' 
    Public Sub AddRefReactant()

        Dim expEntry = CType(DataContext, tblExperiments)

        Dim newProtocolEntry = CreateProtocolItem(ProtocolElementType.RefReactant)
        Dim newRefReact = New tblRefReactants
        Dim currRefReactItem = (From prot In expEntry.tblProtocolItems Where prot.tblRefReactants IsNot Nothing).FirstOrDefault

        With newRefReact
            .GUID = Guid.NewGuid.ToString("D")
            .ProtocolItem = newProtocolEntry
            .Name = If(currRefReactItem Is Nothing, "Reactant", currRefReactItem.tblRefReactants.Name)   'ensure consistent name if multiple portions
            .ResinLoad = currRefReactItem?.tblRefReactants.ResinLoad    'dito for resin load
            .Source = currRefReactItem?.tblRefReactants.Source
        End With

        newProtocolEntry.tblRefReactants = newRefReact

        Dim addReactDlg As New dlgEditRefReactant
        With addReactDlg

            SetExpContentCenterStartupPos(addReactDlg)
            .IsAddingNew = True
            .ReferenceReactant = newRefReact
            .SketchInfo = SketchInfo

            If .ShowDialog() Then

                AddProtocolItem(newProtocolEntry)
                AlignRefReactantProperties(newRefReact)
                UpdateRefReactants()
                RecalculateExperiment()

                RaiseEvent RequestUpdateSketchComponentLabels(Me)

                Dim refs = From prot In expEntry.tblProtocolItems Where prot.ElementType = ProtocolElementType.RefReactant
                If refs.Count = 1 Then
                    UndoEngine.Reset()  'first and required ref.reactant -> suppress the creation of an Undo point
                End If

            End If

        End With

    End Sub


    ''' <summary>
    ''' Add a new reagent
    ''' </summary>
    ''' 
    Public Sub AddReagent()

        Dim newProtocolEntry = CreateProtocolItem(ProtocolElementType.Reagent)
        Dim newReagent = New tblReagents

        With newReagent
            .GUID = Guid.NewGuid.ToString("D")
            .ProtocolItem = newProtocolEntry
            .SpecifiedUnitType = MaterialUnitType.Equivalent
        End With
        newProtocolEntry.tblReagents = newReagent

        Dim addReagentDlg As New dlgEditReagent

        With addReagentDlg

            SetExpContentCenterStartupPos(addReagentDlg)
            .ReagentEntry = newReagent
            .IsAddingNew = True

            If .ShowDialog() Then
                AddProtocolItem(newProtocolEntry)
            End If

        End With

    End Sub


    ''' <summary>
    ''' Add a new solvent
    ''' </summary>
    ''' 
    Public Sub AddSolvent()

        Dim newProtocolEntry = CreateProtocolItem(ProtocolElementType.Solvent)
        Dim newSolvent = New tblSolvents

        With newSolvent
            .GUID = Guid.NewGuid.ToString("D")
            .ProtocolItem = newProtocolEntry
            .SpecifiedUnitType = MaterialUnitType.Volume
        End With
        newProtocolEntry.tblSolvents = newSolvent

        Dim addSolventDlg As New dlgEditSolvent

        With addSolventDlg

            SetExpContentCenterStartupPos(addSolventDlg)
            .IsAddingNew = True
            .SolventEntry = newSolvent

            If .ShowDialog() Then
                AddProtocolItem(newProtocolEntry)
            End If

        End With

    End Sub


    ''' <summary>
    ''' Add a new auxiliary
    ''' </summary>
    ''' 
    Public Sub AddAuxiliary()

        Dim newProtocolEntry = CreateProtocolItem(ProtocolElementType.Auxiliary)
        Dim newAuxiliary = New tblAuxiliaries

        With newAuxiliary
            .GUID = Guid.NewGuid.ToString("D")
            .ProtocolItem = newProtocolEntry
            .SpecifiedUnitType = MaterialUnitType.Volume
        End With
        newProtocolEntry.tblAuxiliaries = newAuxiliary

        Dim addAuxiliaryDlg As New dlgEditAuxiliary

        With addAuxiliaryDlg

            SetExpContentCenterStartupPos(addAuxiliaryDlg)
            .IsAddingNew = True
            .AuxiliaryEntry = newAuxiliary

            If .ShowDialog() Then
                AddProtocolItem(newProtocolEntry)
            End If

        End With

    End Sub


    ''' <summary>
    ''' Add a new comment
    ''' </summary>
    ''' 
    Public Sub AddComment()

        Dim newProtocolEntry = CreateProtocolItem(ProtocolElementType.Comment)
        Dim newComment = New tblComments

        With newComment
            .GUID = Guid.NewGuid.ToString("D")
            .ProtocolItem = newProtocolEntry
        End With
        newProtocolEntry.tblComments = newComment

        Dim newLbItem = AddProtocolItem(newProtocolEntry)
        Dim currComment = WPFToolbox.FindVisualChild(Of CommentContent)(newLbItem)
        currComment.ActivateEdit()

    End Sub


    ''' <summary>
    ''' Add a new workflow separator
    ''' </summary>
    ''' 
    Public Sub AddSeparator(Optional title As String = "Title", Optional activateEdit As Boolean = True)

        Dim newProtocolEntry = CreateProtocolItem(ProtocolElementType.Separator)
        Dim newSeparator = New tblSeparators

        With newSeparator
            .GUID = Guid.NewGuid.ToString("D")
            .ElementType = 0
            .DisplayType = 0
            .Title = title
        End With
        newProtocolEntry.tblSeparators = newSeparator

        Dim newLbItem = AddProtocolItem(newProtocolEntry)
        Dim currSeparator = WPFToolbox.FindVisualChild(Of WorkflowSeparator)(newLbItem)
        If activateEdit Then
            currSeparator.ActivateEdit()
        Else
            currSeparator.Focus()
        End If

    End Sub


    ''' <summary>
    ''' Add a new product
    ''' </summary>
    ''' 
    Public Function AddProduct(Optional prodIndex As Integer? = Nothing) As Boolean

        Dim newProtocolEntry = CreateProtocolItem(ProtocolElementType.Product)
        Dim newProduct = New tblProducts

        With newProduct
            .GUID = Guid.NewGuid.ToString("D")
            .ProtocolItem = newProtocolEntry
            If prodIndex IsNot Nothing Then
                .ProductIndex = prodIndex
            End If
        End With

        newProtocolEntry.tblProducts = newProduct

        Dim addProductDlg As New dlgEditProduct
        With addProductDlg

            SetExpContentCenterStartupPos(addProductDlg)
            .SketchInfo = SketchInfo
            .IsAddingNew = True
            .IsFromPlaceholder = (prodIndex IsNot Nothing)
            .ProductEntry = newProduct

            If .ShowDialog() Then
                AddProtocolItem(newProtocolEntry)
                ELNCalculations.UpdateExperimentTotals(CType(DataContext, tblExperiments))
                RaiseEvent RequestUpdateSketchComponentLabels(Me)
                AutoSave(noUndoPoint:=True)
                Return True
            Else
                Return False
            End If

        End With

    End Function



    ''' <summary>
    ''' Gets the maximum allowed single file size in MB for embedding
    ''' </summary>
    ''' 
    Public Const MaxSingleFileMB As Double = 10


    ''' <summary>
    ''' Gets the maximum allowed single file size in MB for embedding
    ''' </summary>
    ''' 
    Public Const MaxTotalFileMB As Double = 50


    ''' <summary>
    ''' Adds one or multiple files, as selected from a file open dialog.
    ''' </summary>
    ''' 
    Public Sub AddFiles()

        Dim dlgSelectFile As New OpenFileDialog

        With dlgSelectFile

            .Title = "Select the images and documents to embed ..."
            .Multiselect = True

            If My.Settings.LastFileEmbedDir = "" Then
                .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            Else
                .InitialDirectory = My.Settings.LastFileEmbedDir
            End If

            If .ShowDialog Then

                Dim insertPos As Integer
                If lstProtocol.SelectedItem IsNot Nothing Then
                    insertPos = CType(lstProtocol.SelectedItem, tblProtocolItems).SequenceNr 'multiple selection: returns last item
                Else
                    insertPos = lstProtocol.Items.Count - 1
                End If

                EmbedFileCandidates(.FileNames.ToList, insertPos)   'includes auto-save

                My.Settings.LastFileEmbedDir = Path.GetDirectoryName(.FileName)

            End If
        End With

    End Sub


    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="srcFiles"></param>
    ''' <param name="insertPos"></param>
    ''' 
    Public Sub EmbedFileCandidates(srcFiles As List(Of String), insertPos As Integer)

        Dim errorList As New List(Of String)

        For Each filePath In srcFiles

            Dim droppedFileType As EmbeddedFileType

            'get embedding type of dropped file
            Dim imgExtList As New List(Of String) From {".jpg", ".jpeg", ".png", ".tif", ".tiff", ".gif"}
            If imgExtList.Contains(Path.GetExtension(filePath)) Then
                droppedFileType = EmbeddedFileType.Image
            Else
                droppedFileType = EmbeddedFileType.Document
            End If

            'try to embed the file
            Dim res = EmbedDocument(filePath, droppedFileType, insertPos)

            If res <> EmbeddingResult.Succeeded Then
                Select Case res
                    Case EmbeddingResult.UnsupportedType
                        errorList.Add("=> " + Path.GetFileName(filePath) + " - unsupported file type.")
                    Case EmbeddingResult.SizeExceeded
                        errorList.Add("=> " + Path.GetFileName(filePath) + " - file size exceeded (10 MB).")
                    Case EmbeddingResult.TotalSizeExceeded
                        errorList.Add("=> " + Path.GetFileName(filePath) + " - total file size exceeded (80 MB).")
                    Case EmbeddingResult.NoImageContent
                        errorList.Add("=> " + Path.GetFileName(filePath) + " - no valid image content.")
                End Select
            End If

            insertPos += 1

        Next

        If errorList.Count > 0 Then
            Dim errText = "Following file(s) could not be embedded:" + vbCrLf + vbCrLf
            errText += String.Join(vbCrLf, errorList)
            MsgBox(errText, MsgBoxStyle.Information, "Embedding Errors")
        End If

        If srcFiles.Count > errorList.Count Then

            '- update view
            ProtocolItemsViewSrc.View.Refresh()

            '- select and activate last embedded item
            Dim lbItem = SelectProtocolItem(_LastEmbeddedProtocolItem)
            lbItem.IsSelected = True

            If _LastEmbeddedProtocolItem.ElementType = ProtocolElementType.File Then
                Dim embedFile = WPFToolbox.FindVisualChild(Of FileContent)(lbItem)
                embedFile.ActivateEdit()
            Else
                Dim embedImage = WPFToolbox.FindVisualChild(Of ImageContent)(lbItem)
                embedImage.ActivateEdit()
            End If

            AutoSave()

        End If

        '    lstProtocol.Focus()

    End Sub


    Private _LastEmbeddedProtocolItem As tblProtocolItems


    ''' <summary>
    ''' Embeds the specified file, if the file size restrictions are met.
    ''' </summary>
    ''' 
    Private Function EmbedDocument(filePath As String, embedFileType As EmbeddedFileType,
      Optional insertIndex As Integer? = Nothing) As EmbeddingResult

        _LastEmbeddedProtocolItem = Nothing

        Dim info = New IO.FileInfo(filePath)

        If info.Exists Then

            'some file types can't be opened later as PDF attachments for security reasons 
            If FileContent.PdfFileTypeExclusions.Contains(info.Extension) Then
                Return EmbeddingResult.UnsupportedType
            End If

            'test for presence of image content
            If embedFileType = EmbeddedFileType.Image Then
                Try
                    Using newImage = System.Drawing.Image.FromFile(filePath)
                    End Using
                Catch ex As Exception
                    Return EmbeddingResult.NoImageContent
                End Try
            End If

            'limit single file size
            Dim docMB = info.Length / (1024.0 * 1024.0)
            If docMB > MaxSingleFileMB Then
                Return EmbeddingResult.SizeExceeded
            End If

            'limit total embedded file sizes per experiment
            Dim totalMB = docMB + TotalEmbeddedFileSize()
            If totalMB > MaxTotalFileMB Then
                Return EmbeddingResult.TotalSizeExceeded
            End If

            Dim itemType = If(embedFileType = EmbeddedFileType.Document, ProtocolElementType.File, ProtocolElementType.Image)
            Dim newProtocolEntry = CreateProtocolItem(itemType)
            Dim newFileEntry = New tblEmbeddedFiles

            'get byte array of file, even if locked by another application
            Dim isPortraitMode As Integer = 0
            Dim fileBytes = SerializeFile(filePath, embedFileType, maxImagePixels:=1600, isPortraitMode)

            With newFileEntry
                .GUID = Guid.NewGuid.ToString("D")
                .ProtocolItem = newProtocolEntry
                .FileType = embedFileType
                .FileName = Path.GetFileName(filePath)
                .FileSizeMB = docMB
                .FileBytes = fileBytes
                .FileComment = Path.GetFileName(filePath)
                .IconImage = FileContent.GetFileIconBytes(filePath)
                .IsPortraitMode = isPortraitMode
                .IsRotated = If(isPortraitMode, 1, 0)
                .SHA256Hash = ELNCryptography.GetSHA256Hash(.FileBytes) 'for digital signature
            End With

            newProtocolEntry.tblEmbeddedFiles = newFileEntry
            AddProtocolItem(newProtocolEntry, insertIndex, doSave:=False)
            _LastEmbeddedProtocolItem = newProtocolEntry

            Return EmbeddingResult.Succeeded

        Else

            Return EmbeddingResult.FileNotFound

        End If

    End Function


    ''' <summary>
    ''' Serializes either a file or an image to be embedded, if embedding an image also reducing image size according to 
    ''' the specified maxPixels.
    ''' </summary>
    ''' <param name="filePath">Path to the file to be serialized.</param>
    ''' <param name="fileType">The EmbeddedFileType of the file. </param>
    ''' <param name="maxImagePixels">The maximum image size in case of an image. This 
    ''' parameter is ignored for EmbeddedFileType.Document.</param>
    ''' <param name="isPortraitMode">ByRef: Set to 1 if the image is vertically oriented, to 0 otherwise. 
    ''' This parameter is ignored for EmbeddedFileType.Document.</param>
    ''' 
    Private Function SerializeFile(filePath As String, fileType As EmbeddedFileType, maxImagePixels As Integer,
       ByRef isPortraitMode As Integer) As Byte()

        Try

            If fileType = EmbeddedFileType.Document Then
                Return File.ReadAllBytes(filePath)
            Else
                Dim bmImage = WPFToolbox.BitmapImageFromFile(filePath, maxImagePixels)
                isPortraitMode = If(bmImage.Width < bmImage.Height, 1, 0)
                Return WPFToolbox.BitmapImageToBytes(bmImage, Path.GetExtension(filePath))
            End If

        Catch ex As Exception

            'ReadAllBytes can't serialize locked files (e.g. open office docs). However, file copy works
            'just fine, so the file is copied to a temp file before serializing ... 

            Dim tmpPath = FileContent.TempDocsPath + "\copy.tmp"
            Dim res As Byte()

            File.Copy(filePath, tmpPath, True)
            If fileType = EmbeddedFileType.Document Then
                res = File.ReadAllBytes(tmpPath)
            Else
                Dim bmImage = WPFToolbox.BitmapImageFromFile(tmpPath, maxImagePixels)
                isPortraitMode = If(bmImage.Width < bmImage.Height, 1, 0)
                Return WPFToolbox.BitmapImageToBytes(bmImage, Path.GetExtension(tmpPath))
            End If

            File.Delete(tmpPath)
            Return res

        End Try

    End Function


    ''' <summary>
    ''' Gets the total size, in MB, of all files currently embedded in the experiment
    ''' </summary>
    ''' 
    Private Function TotalEmbeddedFileSize() As Double

        Dim expEntry = CType(Me.DataContext, tblExperiments)

        Dim embeddedFiles = From prot In expEntry.tblProtocolItems Where prot.ElementType = ProtocolElementType.File
                            Select prot.tblEmbeddedFiles

        If embeddedFiles.Any Then
            Return Aggregate prot In expEntry.tblProtocolItems Where
                 prot.ElementType = ProtocolElementType.File Into Sum(prot.tblEmbeddedFiles.FileSizeMB)
        Else
            Return 0
        End If

    End Function


    ''' <summary>
    ''' Adds the specified protocolEntry to the data model, selects its parent ListBoxItem in 
    ''' the protocol ListBox and adds an auto-save point.
    ''' </summary>
    ''' <param name="protocolEntry">The tblProtocolEntry to add.</param>
    ''' <param name="insertPos">The insert position, if not at the end of the list.</param>
    ''' <param name="doSave">Set to false if multiple items are to added at the same time, e.g. multiple dropped files. </param>
    ''' <returns>The protocol ListBoxItem containing the protocolEntry.</returns>
    ''' 
    Private Function AddProtocolItem(protocolEntry As tblProtocolItems, Optional insertPos As Integer? = Nothing,
       Optional doSave As Boolean = True) As ListBoxItem

        'normalize sequence, to be on safe side
        UpdateElementSequenceNumbers()

        'get insert index
        Dim insertIndex As Integer
        If insertPos Is Nothing Then
            If lstProtocol.SelectedItem IsNot Nothing AndAlso AdditionToolbar.DoInsert Then
                'insert element
                insertIndex = CType(lstProtocol.SelectedItem, tblProtocolItems).SequenceNr  'multiple selection: returns last item
            Else
                'append element
                insertIndex = lstProtocol.Items.Count - 1
            End If
        Else
            'insert at custom insert pos
            insertIndex = insertPos
        End If

        'renumber protocol item sequence
        For Each item As tblProtocolItems In lstProtocol.Items
            If item.SequenceNr > insertIndex Then
                item.SequenceNr += 1
            End If
        Next

        'assign sequence number
        protocolEntry.SequenceNr = insertIndex + 1

        'add to experiment
        Dim expEntry = CType(DataContext, tblExperiments)
        expEntry.tblProtocolItems.Add(protocolEntry)

        If doSave Then
            ProtocolItemsViewSrc.View.Refresh()
            Dim selItem = SelectProtocolItem(protocolEntry)
            AutoSave()
            Return selItem
        Else
            Return Nothing
        End If

    End Function


    ''' <summary>
    ''' Selects the ListBoxItem containing the protocolEntry.
    ''' </summary>
    ''' <param name="protocolEntry">The tblProtocolEntry to add.</param>
    ''' <returns>The protocol ListBoxItem containing the protocolEntry.</returns>
    ''' 
    Public Function SelectProtocolItem(protocolEntry As tblProtocolItems) As ListBoxItem

        With lstProtocol
            .UpdateLayout()
            .UnselectAll()
        End With

        Dim newLbItem As ListBoxItem = lstProtocol.ItemContainerGenerator.ContainerFromItem(protocolEntry)

        With newLbItem
            .IsSelected = True
            .Focus()
        End With

        WPFToolbox.ScrollItemIntoView(scrlProtocol, lstProtocol, newLbItem)

        Return newLbItem

    End Function


    ''' <summary>
    ''' Creates a parent ProtocolItem.
    ''' </summary>
    ''' <param name="itemType">The ProtocolElementType of the contained child element.</param>
    ''' 
    Private Function CreateProtocolItem(itemType As ProtocolElementType) As tblProtocolItems

        Dim expEntry = CType(DataContext, tblExperiments)

        Dim newItem As New tblProtocolItems
        With newItem
            .GUID = Guid.NewGuid.ToString("D")  'format containing hyphens "-"
            .ExperimentID = expEntry.ExperimentID
            .ElementType = itemType
            .Experiment = expEntry
            .SequenceNr = GetMaxItemSequenceNr(expEntry) + 1
        End With

        Return newItem

    End Function


    Public Sub RefreshItems()

        UpdateElementSequenceNumbers()
        ProtocolItemsViewSrc.View.Refresh()

    End Sub


    ''' <summary>
    ''' Deletes the selected ProtocolItems.
    ''' </summary>
    ''' 
    Public Sub DeleteSelectedProtocolItems()

        Dim selItems = lstProtocol.SelectedItems

        If selItems.Count > 0 Then

            Dim expEntry = CType(DataContext, tblExperiments)

            Dim totalRefReactCount = RefReactantCount(expEntry.tblProtocolItems)
            Dim selectedRefReactCount = RefReactantCount(selItems)

            If selectedRefReactCount < totalRefReactCount Then

                For Each item As tblProtocolItems In selItems
                    item.Experiment.tblProtocolItems.Remove(item)
                Next

                UpdateRefReactants()

                Dim skipRecalcDlg = (selectedRefReactCount = 0)
                RecalculateExperiment(skipRecalcDlg)

                UpdateElementSequenceNumbers()
                RaiseEvent RequestUpdateSketchComponentLabels(Me)

                AutoSave()

                ProtocolItemsViewSrc.View.Refresh()
                UnselectAll()

            Else

                MsgBox("Can't delete, since at least one" + vbCrLf +
                        "reference reactant is required! ", MsgBoxStyle.Information + MsgBoxStyle.OkOnly, "Validation")

            End If

        End If

    End Sub


    ''' <summary>
    ''' Recalculates the experiment stoichiometry due to changes in the reference reactant and/pr product mmols.
    ''' </summary>
    ''' 
    Public Sub RecalculateExperiment(Optional skipRecalcDialog As Boolean = False)

        Dim recalcMode As RecalculationMode

        If Not (skipRecalcDialog OrElse OnlyRefReactantPresent()) Then

            Dim dlgCalcMode As New dlgRecalcMode

            With dlgCalcMode
                .Owner = WPFToolbox.FindVisualParent(Of Window)(Me)
                .ShowDialog()
                If .rdoKeepEquivs.IsChecked Then
                    recalcMode = RecalculationMode.KeepEquivalents
                ElseIf .rdoKeepAmounts.IsChecked Then
                    recalcMode = RecalculationMode.KeepAmounts
                Else
                    recalcMode = RecalculationMode.KeepAsSpecified
                End If
            End With

        Else

            recalcMode = RecalculationMode.KeepAmounts

        End If

        Dim expEntry = CType(DataContext, tblExperiments)
        ELNCalculations.RecalculateMaterials(expEntry, recalcMode)

        RaiseEvent RequestUpdateSketchComponentLabels(Me)

    End Sub


    ''' <summary>
    ''' Re-assigns the protocol item sequence numbers towards a contiguous sequence, e.g. to
    ''' remove sequence numbering gaps after an item deletion.
    ''' </summary>
    ''' 
    Private Sub UpdateElementSequenceNumbers()

        Dim expEntry = CType(DataContext, tblExperiments)
        Dim pos = 0

        Dim protocolItems = From prot In expEntry.tblProtocolItems Order By prot.SequenceNr Ascending
        For Each item In protocolItems
            item.SequenceNr = pos
            pos += 1
        Next

    End Sub


    ''' <summary>
    ''' Updates the total amount of all portions of the reference reactant. 
    ''' </summary>
    ''' <param name="expEntry">Optional: The affected experiment. If nothing is specified, 
    ''' then the affected experiment is the source.</param>
    ''' 
    Public Sub UpdateRefReactants(Optional expEntry As tblExperiments = Nothing)

        If expEntry Is Nothing Then
            expEntry = CType(DataContext, tblExperiments)
        End If

        Dim refs = From prot In expEntry.tblProtocolItems Where prot.ElementType = ProtocolElementType.RefReactant

        For Each ref In refs
            ELNCalculations.RecalculateRefReactant(ref.tblRefReactants)
        Next

        With expEntry
            .RefReactantGrams = ELNCalculations.GetTotalRefReactantGrams(expEntry)
            .RefReactantMMols = ELNCalculations.GetTotalRefReactantMmols(expEntry)
        End With

    End Sub


    ''' <summary>
    ''' Gets the number of reference reactant portions present in the protocol
    ''' </summary>
    '''
    Private Function RefReactantCount(selItems As IList) As Integer

        ' Dim expEntry = CType(DataContext, tblExperiments)

        Dim protocolItems = selItems.Cast(Of tblProtocolItems).ToList

        Return Aggregate prot In protocolItems Where
             prot.ElementType = ProtocolElementType.RefReactant Into Count()

    End Function



    ''' <summary>
    ''' Gets the number of protocol elements which are not the reference reactant.
    ''' </summary>
    ''' 
    Private Function OnlyRefReactantPresent() As Boolean

        Dim expEntry = CType(DataContext, tblExperiments)

        Dim otherCount = Aggregate prot In expEntry.tblProtocolItems Where
              prot.ElementType = ProtocolElementType.Reagent OrElse
              prot.ElementType = ProtocolElementType.Solvent OrElse
              prot.ElementType = ProtocolElementType.Auxiliary Into Count()

        Return otherCount = 0

    End Function


    ''' <summary>
    ''' Gets the currently highest protocol item sequence number within the specified experiment.  
    ''' </summary>
    ''' 
    Private Function GetMaxItemSequenceNr(experimentEntry As tblExperiments) As Integer

        If experimentEntry.tblProtocolItems.Count > 0 Then
            Return Aggregate prot In experimentEntry.tblProtocolItems Into Max(prot.SequenceNr)
        Else
            Return -1   'first item
        End If

    End Function


    ''' <summary>
    ''' Assigns assigns the specified material name and resin load to all reference reactant entries 
    ''' already present in the protocol (if any), towards consistent properties.
    ''' </summary>
    ''' <param name="newName">The reference reactant name to apply to all of its protocol occurrences.</param>
    ''' 
    Private Sub AlignRefReactantProperties(newReactEntry As tblRefReactants)

        Dim expEntry = CType(DataContext, tblExperiments)

        Dim refSiblings = From prot In expEntry.tblProtocolItems Where prot.tblRefReactants IsNot Nothing
        For Each ref In refSiblings
            ref.tblRefReactants.Name = newReactEntry.Name
            ref.tblRefReactants.ResinLoad = newReactEntry.ResinLoad
        Next

    End Sub


    ''' <summary>
    ''' Determines if changes to the resin attachment of the reference reactant (portions) occurred, requests  
    ''' a resin load value if required, and updates it for all portions.
    ''' </summary>
    ''' 
    Public Sub UpdateRefReactResinInfo(expEntry As tblExperiments, isResinAttached As Boolean)

        Dim refReactItems = From protItem In expEntry.tblProtocolItems Where protItem.ElementType = ProtocolElementType.RefReactant

        'assumes that all refReact portions were assigned identical resin properties
        Dim firstRefReact = refReactItems.First.tblRefReactants

        If firstRefReact.ResinLoad IsNot Nothing Xor isResinAttached Then

            Dim resinLoad As Double? = Nothing

            If firstRefReact.ResinLoad Is Nothing Then

                'display edit refReact dialog
                Dim addReactDlg As New dlgEditRefReactant
                With addReactDlg

                    SetExpContentCenterStartupPos(addReactDlg)
                    .IsAddingNew = False
                    .SketchInfo = SketchInfo
                    .ReferenceReactant = firstRefReact
                    .btnCancel.IsEnabled = False

                    .ShowDialog()

                    AlignRefReactantProperties(.ReferenceReactant)
                    UpdateRefReactants()

                End With

            Else

                'resin was removed
                For Each refItem In refReactItems
                    refItem.tblRefReactants.ResinLoad = Nothing
                Next

            End If

        End If

    End Sub


    ''' <summary>
    ''' Determines if changes to the resin attachment of the reference product (portions) occurred, requests  
    ''' a resin load value if required, and updates it for all portions.
    ''' </summary>
    ''' 
    Public Sub UpdateProductsResinInfo(expEntry As tblExperiments)

        'TODO: Consider multiple refProduct portions AND multiple *additional* (i.e. non-ref.) products!

        Dim prodItems = From protItem In expEntry.tblProtocolItems Where protItem.ElementType = ProtocolElementType.Product
        If prodItems.Any Then

            For i = 0 To SketchInfo.Products.Count - 1

                'enumerate all portions of a specific sketch product

                Dim prodIndex = i
                Dim isResinAttached = SketchInfo.Products(prodIndex).IsAttachedToResin
                Dim prodEntries = From item In prodItems Where item.tblProducts IsNot Nothing AndAlso item.tblProducts.ProductIndex = prodIndex
                If prodEntries.Any Then

                    Dim prodEntry = prodEntries.First.tblProducts

                    If prodEntry.ResinLoad IsNot Nothing Xor isResinAttached Then

                        Dim resinLoad As Double? = Nothing

                        If prodEntry.ResinLoad Is Nothing Then

                            'display edit refReact dialog
                            Dim addProductDlg As New dlgEditProduct
                            With addProductDlg
                                SetExpContentCenterStartupPos(addProductDlg)
                                .IsAddingNew = False
                                .SketchInfo = SketchInfo
                                .ProductEntry = prodEntry
                                .btnCancel.IsEnabled = False

                                .ShowDialog()
                                resinLoad = .ProductEntry.ResinLoad

                            End With
                        End If

                        For Each prodItem In prodEntries
                            prodItem.tblProducts.ResinLoad = resinLoad  'all portions of the same sketch product are assigned the same resin load
                        Next

                    End If
                End If
            Next
        End If

    End Sub


End Class



Public Class ProtocolDropHandler

    '-------------------------------------------------------------------------
    ' Drag & Drop logic (Gong: https://github.com/punker76/gong-wpf-dragdrop) 
    ' (implemented with NuGet gong-wpf-dragdrop package)
    '--------------------------------------------------------------------------

    Implements IDropTarget

    Private Sub IDropTarget_DragOver(dropInfo As IDropInfo) Implements IDropTarget.DragOver

        dropInfo.Effects = DragDropEffects.Move
        dropInfo.DropTargetAdorner = DropTargetAdorners.Insert

    End Sub


    Private Sub IDropTarget_Drop(dropInfo As IDropInfo) Implements IDropTarget.Drop

        'core functionality

        If TypeOf dropInfo.Data Is tblProtocolItems Then
            RearrangeDropSequence(dropInfo)

        ElseIf dropInfo.Data.GetDataPresent(DataFormats.FileDrop) Then
            InsertDroppedFiles(dropInfo)

        End If

    End Sub


    Private Sub IDropTarget_DragEnter(dropInfo As IDropInfo) Implements IDropTarget.DragEnter

    End Sub


    Private Sub IDropTarget_DragLeave(dropInfo As IDropInfo) Implements IDropTarget.DragLeave

    End Sub


    ''' <summary>
    ''' Rearranges the the sequence numbers of the experiment ProtocolElement collection to reflect the 
    ''' sequence oder of the elements after the DragDrop move operation.
    ''' </summary>
    ''' <param name="dropInfo">DropInfo obtained from the IDropTarget.DropTarget event.</param>
    ''' 
    Private Shared Sub RearrangeDropSequence(dropInfo As IDropInfo)

        If TypeOf dropInfo.Data Is tblProtocolItems AndAlso TypeOf dropInfo.VisualTarget Is ListBox Then

            Dim dragItem = CType(dropInfo.Data, tblProtocolItems)

            Dim elementList = dropInfo.TargetCollection
            Dim origPos = dragItem.SequenceNr

            Dim insertPos = dropInfo.InsertIndex
            If insertPos > 0 AndAlso insertPos > origPos Then
                insertPos -= 1  'append to bottom
            End If

            'Normalize sequence numbers to prevent numbering gaps, e.g. after deletions
            '(note: TargetCollection already is ordered by SequenceNr)
            Dim pos As Integer = 0
            For Each item As tblProtocolItems In elementList
                If item.SequenceNr <> pos Then
                    item.SequenceNr = pos
                End If
                pos += 1
            Next

            'Remove dragged
            For Each item As tblProtocolItems In elementList
                If item.SequenceNr > origPos Then
                    item.SequenceNr -= 1    'move lower up
                End If
            Next

            'Insert dragged
            For Each item As tblProtocolItems In elementList
                If item.SequenceNr >= insertPos Then
                    item.SequenceNr += 1    'move lower down
                End If
            Next

            dragItem.SequenceNr = insertPos

            Dim lbProtocol = CType(dropInfo.VisualTarget, ListBox)
            Dim parentProtocol = WPFToolbox.FindVisualParent(Of Protocol)(lbProtocol)
            parentProtocol.AutoSave()

        End If

    End Sub


    Private Shared Sub InsertDroppedFiles(dropInfo As IDropInfo)

        If TypeOf dropInfo.VisualTarget Is ListBox Then

            Dim myFiles = CType(dropInfo.Data.GetData(DataFormats.FileDrop), String()).ToList
            Dim insertPos = dropInfo.InsertIndex - 1
            Dim lbProtocol = CType(dropInfo.VisualTarget, ListBox)
            Dim parentProtocol = WPFToolbox.FindVisualParent(Of Protocol)(lbProtocol)

            parentProtocol.EmbedFileCandidates(myFiles, insertPos)

        End If

    End Sub

End Class


Friend Class DefaultDragDropAdornerConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim wfState As WorkflowStatus = value
        Return wfState <> WorkflowStatus.Finalized

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


