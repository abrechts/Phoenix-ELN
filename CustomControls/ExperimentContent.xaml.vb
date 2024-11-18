Imports System.Globalization
Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.Win32


Public Class TabItemInfo

    Public Property UndoRedoEngine As UndoRedo
    Public Property VerticalScrollPos As Double

End Class


Public Class ExperimentContent

    'Interface for providing VisualPaginator with correct page break locations within the control
    Implements PrintPageTemplate.VisualPaginator.IVisualPaginator


    ''' <summary>
    ''' Fires whenever the ExperimentContent is assigned a different experiment
    ''' </summary>
    '''
    Public Shared Event ExperimentContextChanged(sender As Object, newExpEntry As tblExperiments)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        AddHandler SketchArea.SketchSourceChanged, AddressOf SketchArea_SketchSourceChanged

    End Sub


    ''' <summary>
    ''' Sets or gets the experiments database context.
    ''' </summary>
    ''' 
    Public Shared Property DbContext As ElnDbContext


    ''' <summary>
    ''' Sets or gets the single ContentPresenter of the tabExperiments TabControl. Note that there's only one 
    ''' ¨content presenter for all TabItems in a data bound TabControl.
    ''' </summary>
    ''' 
    Public Shared Property TabExperimentsPresenter As ContentPresenter


    Private Sub DataContext_Changed() Handles Me.DataContextChanged

        If Me.DataContext IsNot Nothing AndAlso TypeOf Me.DataContext Is tblExperiments Then

            Dim expTabCtrl = WPFToolbox.FindVisualParent(Of TabControl)(Me)

            If expTabCtrl IsNot Nothing Then    'is nothing for printing/PDF

                Dim thisTab As TabItem = expTabCtrl.ItemContainerGenerator.ContainerFromItem(expTabCtrl.SelectedItem)

                If thisTab.Tag Is Nothing Then
                    Dim tabInfo As New TabItemInfo
                    With tabInfo
                        .UndoRedoEngine = New UndoRedo(DbContext, CType(Me.DataContext, tblExperiments))
                        .VerticalScrollPos = 0
                    End With
                    thisTab.Tag = tabInfo
                End If

                With ExpProtocol()
                    .UndoEngine = CType(thisTab.Tag, TabItemInfo).UndoRedoEngine
                    .UndoEngine.UpdateCanUndoRedo()
                    .VerticalScrollOffset = CType(thisTab.Tag, TabItemInfo).VerticalScrollPos
                    .Focus()
                End With

            End If

            RaiseEvent ExperimentContextChanged(Me, Me.DataContext)

        End If

    End Sub


    ''' <summary>
    ''' Handle keyboard shortcuts
    ''' </summary>
    '''
    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        Select Case e.Key

            Case Key.Delete

                'skip DEL key processing if currently in TextBox or RichTextBox editing
                If TypeOf e.OriginalSource IsNot RichTextBox AndAlso TypeOf e.OriginalSource IsNot TextBox Then
                    ExpProtocol.DeleteSelectedProtocolItems()
                    e.Handled = True
                End If

            Case Key.Escape

                If Not (TypeOf e.OriginalSource Is RichTextBox OrElse TypeOf e.OriginalSource Is TextBox) Then
                    'clear list selection
                    ExpProtocol.lstProtocol.UnselectAll()
                    e.Handled = True
                Else
                    'commit changes to TextBox or RichTextBox in edit mode
                    If e.OriginalSource.IsKeyboardFocused Then
                        Keyboard.ClearFocus()
                        ExpProtocol.lstProtocol.Focus()
                        e.Handled = True
                    End If
                End If

        End Select

    End Sub


    ''' <summary>
    ''' Updates the data context for the reaction sketch (for undo/redo)
    ''' </summary>
    ''' 
    Public Sub RefreshSketch()

        pnlSketch.DataContext = Nothing
        pnlSketch.DataContext = Me.DataContext

    End Sub


    ''' <summary>
    ''' Gets the experiment protocol.
    ''' </summary>
    ''' 
    Public Function ExpProtocol() As Protocol

        Return pnlProtocol

    End Function


    ''' <summary>
    ''' Gets the experiment sketch panel
    ''' </summary>
    ''' 
    Public Function SketchPanel() As SketchArea

        Return pnlSketch

    End Function


    ''' <summary>
    ''' Updates the sketch component labels
    ''' </summary>
    ''' 
    Public Sub UpdateComponentLabels()

        pnlSketch.SetComponentLabels()

    End Sub


    Private Sub protocol_RequestUpdateComponentLabels(sender As Object) Handles pnlProtocol.RequestUpdateSketchComponentLabels

        With pnlSketch
            .SetComponentLabels()
        End With

    End Sub


    Private Sub SketchArea_SketchSourceChanged(sender As Object, skInfo As SketchResults)  'shared event

        pnlProtocol.SketchInfo = skInfo

    End Sub


    Private Sub pnlSketch_SketchEdited(sender As Object, isReactantModified As Boolean) Handles _
      pnlSketch.SketchEdited

        Dim expEntry = CType(Me.DataContext, tblExperiments)

        Dim refProdEntries = From protItem In expEntry.tblProtocolItems Where protItem.ElementType = ProtocolElementType.Product
        Dim isRefReactAttached = pnlSketch.SketchInfo.Reactants.First.IsAttachedToResin
        Dim isRefProdAttached = pnlSketch.SketchInfo.Products.First.IsAttachedToResin

        If pnlSketch.SketchInfo IsNot Nothing Then

            If expEntry.tblProtocolItems.Count = 0 Then
                'new experiment
                With pnlProtocol
                    .addToolbar.SetCurrentValue(VisibilityProperty, Visibility.Collapsed) 'does not break current binding
                    .AddRefReactant()
                    .AddSeparator("Reaction", activateEdit:=False, insertPos:=-1)
                    .addToolbar.SetCurrentValue(VisibilityProperty, Visibility.Visible)
                    .RecalculateExperiment(False)
                End With
            Else
                'existing experiment
                With pnlProtocol
                    .UpdateRefReactResinInfo(expEntry, isRefReactAttached)  'changes to refReact resin attachments (on/off)
                    .UpdateProductsResinInfo(expEntry) 'changes to products resin attachments (on/off)
                    .RecalculateExperiment(Not isReactantModified)
                End With
            End If

            ExpProtocol.AutoSave()

        End If

    End Sub


    Private Sub btnFinalize_Click() Handles btnFinalize.Click

        Dim expEntry = CType(Me.DataContext, tblExperiments)
        ExpProtocol.ChangeWorkflowState(expEntry, WorkflowStatus.Finalized)

    End Sub


    ''' <summary>
    ''' Adds a new experiment to the specified project, by creating a new one, cloning from the current one, 
    ''' or by importing it from a file. 
    ''' </summary>
    ''' <returns>
    ''' False, if the action was cancelled by the user, otherwise true.
    ''' </returns>
    ''' 
    Public Function CreateExperiment(dbContext As ElnDataContext, expNavTree As ExperimentTree, dstProject As tblProjects) As Boolean

        Dim result As Boolean = False

        Dim currExp = CType(Me.DataContext, tblExperiments)
        Dim replaceEmpty As Boolean = False

        'check if current experiment is empty
        '------------------------------------
        Dim latestExp = ExperimentBase.GetLatestExperiment(dstProject.User)
        If latestExp IsNot Nothing AndAlso latestExp.RxnSketch Is Nothing Then

            If currExp Is latestExp Then
                'current exp is empty
                MsgBox("Can't create another experiment while" + vbCrLf +
                       "your current one is empty. - Select" + vbCrLf +
                       "another experiment and then" + vbCrLf +
                       "try again.", MsgBoxStyle.Information + MsgBoxStyle.OkCancel, "Experiment Creation")
                Return False
            Else
                replaceEmpty = True
            End If
        End If


        'add or clone an experiment
        '--------------------------

        Dim newDlg As New dlgNewExperiment
        With newDlg

            .Owner = WPFToolbox.FindVisualParent(Of Window)(Me)
            .CloneMethod = CloneType.EmptyExperiment
            .CurrentExperiment = currExp
            .TargetProject = dstProject

            If .ShowDialog() Then

                'silently remove last empty experiment if present (will be replaced by new one)
                If replaceEmpty Then
                    latestExp.Project.tblExperiments.Remove(latestExp)
                    latestExp.User.tblExperiments.Remove(latestExp)
                    dbContext.SaveChanges()
                End If

                If .CloneMethod <> CloneType.FromImport Then

                    'Create new or clone current experiment 
                    '--------------------------------------

                    Dim clonedExp = ExperimentBase.CloneExperiment(dbContext, currExp, .TargetProject, .CloneMethod, .SkipEmbeddedDocs)
                    clonedExp.DisplayIndex = Nothing

                    expNavTree.RefreshItems()
                    expNavTree.SelectExperiment(clonedExp)

                    'After SelectExperiment this ExperimentContent is removed from the visual tree
                    'since replaced by the new one -> access the new ExperimentContent now!

                    Dim newExpContent = WPFToolbox.FindVisualChild(Of ExperimentContent)(TabExperimentsPresenter)

                    If .CloneMethod <> CloneType.EmptyExperiment Then

                        'Sketch-only clone handling:

                        If .CloneMethod = CloneType.SketchOnly Then

                            With newExpContent
                                .pnlProtocol.addToolbar.SetCurrentValue(VisibilityProperty, Visibility.Collapsed) 'does not break current binding
                                .pnlProtocol.AddRefReactant()
                                .pnlProtocol.AddSeparator("Reaction", activateEdit:=False, insertPos:=-1)
                                .pnlProtocol.addToolbar.SetCurrentValue(VisibilityProperty, Visibility.Visible)
                            End With

                        ElseIf .CloneMethod = CloneType.NextStepSketch Then

                            Dim origInfo = DrawingEditor.GetSketchInfo(currExp.RxnSketch)
                            If origInfo IsNot Nothing Then

                                clonedExp.RxnSketch = origInfo.CreateNextStepRxnXML()
                                Dim skArea As New SketchArea
                                skArea.EditSketch(clonedExp)

                                With newExpContent
                                    .pnlProtocol.addToolbar.SetCurrentValue(VisibilityProperty, Visibility.Collapsed) 'does not break current binding
                                    .pnlProtocol.AddRefReactant()
                                    .pnlProtocol.AddSeparator("Reaction", activateEdit:=False, insertPos:=-1)
                                    .pnlProtocol.addToolbar.SetCurrentValue(VisibilityProperty, Visibility.Visible)
                                End With

                            End If

                        ElseIf .CloneMethod = CloneType.FullExperiment Then

                            'scale experiment
                            Dim scaleFactor = .ScaleFactor
                            Dim res = From item In clonedExp.tblProtocolItems Where item.tblRefReactants IsNot Nothing
                            For Each protItem In res
                                protItem.tblRefReactants.Grams *= scaleFactor
                            Next

                            newExpContent.pnlProtocol.UpdateRefReactantTotals(clonedExp)
                            ELNCalculations.RecalculateMaterials(clonedExp, RecalculationMode.KeepEquivalents)

                            ELNCalculations.UpdateExperimentTotals(clonedExp)
                            newExpContent.pnlSketch.SetComponentLabels()

                            newExpContent.pnlProtocol.UnselectAll()

                            MsgBox("Cloning succeeded!", MsgBoxStyle.Information, "Cloning")

                        End If

                    End If

                    newExpContent.pnlProtocol.AutoSave(, noUndoPoint:=True)

                    result = True

                Else

                    'Import from file
                    '----------------

                    Dim openFileDlg As New OpenFileDialog
                    With openFileDlg

                        If My.Settings.LastImportDir = "" Then
                            .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                        Else
                            .InitialDirectory = My.Settings.LastImportDir
                        End If

                        .Filter = "ELN export file (*.exp)|*.exp"
                        .Title = "Import an experiment export file ..."

                        If .ShowDialog Then

                            Dim importExp = ExperimentBase.ImportExperiment(dbContext, .FileName, newDlg.TargetProject, dbContext.tblDatabaseInfo.First.CurrAppVersion)

                            If importExp IsNot Nothing Then

                                '   Me.DataContext = importExp
                                importExp.DisplayIndex = Nothing

                                ELNCalculations.UpdateExperimentTotals(importExp)

                                expNavTree.RefreshItems()
                                expNavTree.SelectExperiment(importExp)  'also sets the data context of ExperimentContent to clonedExp

                                'After SelectExperiment this ExperimentContent is removed from the visual tree
                                'since replaced by the new one -> access the new ExperimentContent now!

                                Dim newExpContent = WPFToolbox.FindVisualChild(Of ExperimentContent)(TabExperimentsPresenter)
                                newExpContent.ExpProtocol.AutoSave(, noUndoPoint:=True)

                                MsgBox("Import succeeded!", MsgBoxStyle.Information, "Cloning")
                                result = True

                            End If

                        End If

                        My.Settings.LastImportDir = Path.GetDirectoryName(.FileName)

                    End With

                End If

            End If
        End With

        Return result

    End Function


    ''' <summary>
    ''' Sets the UI element properties to a state suited for printing.
    ''' </summary>
    ''' 
    Public Sub SetPrintUI()


        pnlProtocol.SetPrintUI()

        Dim expEntry = CType(Me.DataContext, tblExperiments)
        If pnlProtocol.IsPrinting Then
            tabFinalized.SelectedIndex = 0
        End If

        Margin = New Thickness(0, -10, 0, 0)

    End Sub


    ''' <summary>
    ''' This printing interface function provides page break information to prevent page breaks in the middle 
    ''' of a protocol element.
    ''' </summary>
    ''' <param name="proposedPageBreakY">Vertical coordinate of proposed page break.</param>
    ''' <returns>Negative y-offset in pixels for reducing proposed offset. </returns>
    ''' 
    Public Function PageBreakBoundaryOffset(proposedPageBreakY As Double) As Double Implements _
       PrintPageTemplate.VisualPaginator.IVisualPaginator.PageBreakBoundaryOffset

        Dim lstProtocol = pnlProtocol.lstProtocol
        Dim protocolPos = lstProtocol.TranslatePoint(Nothing, Me)  'top-left position of table in this control

        'determine intact DATAGRID row offset 
        If Not IsNothing(lstProtocol.ItemsSource) Then

            With Me  'important!!
                .Measure(New Windows.Size)
                .Arrange(New Rect)
            End With

            lstProtocol.UpdateLayout()

            For Each item In lstProtocol.ItemsSource
                Dim protocolItem = CType(lstProtocol.ItemContainerGenerator.ContainerFromItem(item), ListBoxItem)
                Dim itemPos = protocolItem.TranslatePoint(New Windows.Point(0, 0), Me)   'bottom part below page break?
                Dim bottomY = itemPos.Y + protocolItem.ActualHeight + protocolItem.Margin.Top + protocolItem.Margin.Bottom
                If bottomY > proposedPageBreakY Then
                    Dim diff = itemPos.Y - proposedPageBreakY + 1
                    Return diff
                End If
            Next

        End If

        Return 0

    End Function

End Class


Public Class LocationTitleConverter

    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As System.Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert

        If values(0) Is DependencyProperty.UnsetValue OrElse values(1) Is DependencyProperty.UnsetValue OrElse
          values(2) Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim companyName = values(0)
        Dim departmentName = values(1)
        Dim location = values(2)

        Return companyName + " - " + departmentName + " (" + location + ")"

    End Function

    Public Function ConvertBack(value As Object, targetTypes() As System.Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class ExperimentDateConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As System.Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        If value Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim dateStr As String = value
        Dim defaultStr As String = parameter

        If dateStr <> "" Then
            Return dateStr
        Else
            Return defaultStr
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As System.Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class ExperimentStateToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As System.Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        If value Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim expStatus As WorkflowStatus = value

        If expStatus = WorkflowStatus.Finalized Then
            Return If(parameter <> "invert", Visibility.Visible, Visibility.Collapsed)
        Else
            Return If(parameter <> "invert", Visibility.Collapsed, Visibility.Visible)
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As System.Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class ExperimentStateToTabIndexConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As System.Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        If value Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim expStatus As WorkflowStatus = value

        If expStatus = WorkflowStatus.Finalized Then
            Return 0
        Else
            Return 1
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As System.Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class

