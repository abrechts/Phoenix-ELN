﻿
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore

Public Class ProtocolItemBase

    Private WithEvents ParentListBoxItem As ListBoxItem


    Public Sub New()

        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        ParentListBoxItem = WPFToolbox.FindVisualParent(Of ListBoxItem)(Me)

    End Sub


    Public ReadOnly Property ProtocolItemContent()
        Get
            Return itemContent
        End Get
    End Property


    ''' <summary>
    ''' Prevent protocol item selection in finalized experiment.
    ''' </summary>
    '''
    Private Sub Me_PreviewMouseDown(sender As Object, e As RoutedEventArgs) Handles Me.PreviewMouseDown

        If Me.DataContext IsNot BindingOperations.DisconnectedSource Then
            If CType(Me.DataContext, tblProtocolItems).Experiment.WorkflowState = WorkflowStatus.Finalized Then
                e.Handled = True
            End If
        End If

    End Sub


    ''' <summary>
    ''' Prevents context menu on finalized experiment protocol item
    ''' </summary>
    ''' 
    Private Sub contextMnu_BeforeOpen(sender As Object, e As ContextMenuEventArgs) Handles mainBorder.ContextMenuOpening

        If CType(Me.DataContext, tblProtocolItems).Experiment.WorkflowState = WorkflowStatus.Finalized Then
            e.Handled = True
        End If

    End Sub


    'need to manually select parent ListBoxItem if edit elements present

    Private Sub Me_GotKeyboardFocus(sender As Object, e As RoutedEventArgs) Handles Me.GotKeyboardFocus

        Dim parentListBox = WPFToolbox.FindVisualParent(Of ListBox)(ParentListBoxItem)

        If parentListBox IsNot Nothing Then
            ParentListBoxItem.IsSelected = True
        End If

    End Sub


    ''' <summary>
    ''' Disables mnuDuplicate according to protocol element type.
    ''' </summary>
    ''' 
    Private Sub ContextMnu_ContextMenuOpening() Handles Me.ContextMenuOpening

        Dim protItem = CType(itemContent.Content, tblProtocolItems)

        With protItem
            If Not (.tblRefReactants IsNot Nothing OrElse .tblReagents IsNot Nothing OrElse .tblSolvents IsNot Nothing _
             OrElse .tblAuxiliaries IsNot Nothing OrElse .tblProducts IsNot Nothing) Then
                mnuDuplicate.Visibility = Visibility.Collapsed
                sepDuplicate.Visibility = Visibility.Collapsed
            End If
        End With

    End Sub


    ''' <summary>
    ''' Duplicate the selected protocol item by inserting a copy below it.
    ''' </summary>
    ''' 
    Private Sub mnuDuplicate_Click() Handles mnuDuplicate.Click

        Dim parentProtocol = CType(WPFToolbox.FindVisualParent(Of Protocol)(ParentListBoxItem), Protocol)
        parentProtocol.DuplicateProtocolItem(CType(itemContent.Content, tblProtocolItems))

    End Sub


    ''' <summary>
    ''' Delete this protocol item.
    ''' </summary>
    ''' 
    Private Sub mnuDelete_Click() Handles mnuDelete.Click

        Dim parentProtocol = CType(WPFToolbox.FindVisualParent(Of Protocol)(ParentListBoxItem), Protocol)
        parentProtocol.DeleteSelectedProtocolItems()

    End Sub


    ''' <summary>
    ''' Sets or gets the databaseInfo entry
    ''' </summary>
    ''' 
    Public Shared Property DbInfo As tblDatabaseInfo


    ''' <summary>
    ''' Creates a new tblMaterials entry.
    ''' </summary>
    ''' <param name="matType">The material type of the new entry.</param>
    ''' 
    Public Shared Function CreateNewMatDBEntry(matType As MaterialType) As tblMaterials

        Dim newDBMat As New tblMaterials

        With newDBMat
            .GUID = Guid.NewGuid.ToString("D")
            .MatType = matType
            .DatabaseID = DbInfo.GUID
            .Database = DbInfo
        End With

        Return newDBMat

    End Function


    ''' <summary>
    ''' Adds the specified material to the user materials database, if not present so far, or 
    ''' updates its properties in the DB if differing.
    ''' </summary>
    ''' <param name="tempMatDBEntry">The originally matching matDBEntry with possibly modified material properties, 
    ''' or a new matDBEntry without properties. In both cases attached material safety docs are present.</param> />
    ''' 
    Public Shared Sub UpdateMaterialsDB(tempMatDBEntry As tblMaterials, currDocList As List(Of tblDbMaterialFiles),
        matType As MaterialType, matName As String, source As String, density As Double?, purity As Double?,
        molweight As Double?, molarity As Double?)

        If tempMatDBEntry IsNot Nothing Then

            With tempMatDBEntry

                Dim materialHit = (From mat In ProtocolItemBase.DbInfo.tblMaterials Where mat.MatType = matType _
                  AndAlso mat.MatName.Equals(matName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault

                If materialHit Is Nothing Then
                    'add new material
                    .MatName = matName
                    .MatSource = source
                    .Density = density
                    .Molweight = molweight
                    .Molarity = molarity
                    DbInfo.tblMaterials.Add(tempMatDBEntry)
                Else
                    'update existing material
                    If Not CompareNullableDoubles(molarity, .Molarity, "0.00") OrElse
                       Not CompareNullableDoubles(molweight, .Molweight, "0.00") OrElse
                       Not CompareNullableDoubles(density, .Density, "0.00") OrElse
                       Not CompareNullableDoubles(purity, .Purity, "0.0") OrElse .MatSource <> source Then
                        .MatSource = source
                        .Density = density
                        .Purity = purity
                        .Molweight = molweight
                        .Molarity = molarity
                    End If

                End If

            End With

            UpdateAttachedDocs(tempMatDBEntry, currDocList)

        End If

    End Sub


    Private Shared Sub UpdateAttachedDocs(matEntry As tblMaterials, currDocs As List(Of tblDbMaterialFiles))

        'Update attached materials DB documents. Simply replacing the docs collection 
        'would place a huge strain on server sync, since all entries would be marked 
        'for sync by doing so.

        '- remove deleted docs
        For i = matEntry.tblDbMaterialFiles.Count - 1 To 0 Step -1
            Dim origDoc = matEntry.tblDbMaterialFiles(i)
            If Not currDocs.Contains(origDoc) Then
                matEntry.tblDbMaterialFiles.Remove(origDoc)
            End If
        Next

        '- add added docs
        For Each doc In currDocs
            If Not matEntry.tblDbMaterialFiles.Contains(doc) Then
                matEntry.tblDbMaterialFiles.Add(doc)
            End If
        Next


    End Sub


    ''' <summary>
    ''' Gets if the specified nullable double values are identical within the specified decimal point precision.
    ''' </summary>
    ''' <param name="value1">The first double to compare.</param>
    ''' <param name="value2">The second nullable double to compare.</param>
    ''' <param name="formatStr">The rounding format, e.g. '0.00', describing the desired comparison precision.</param>
    ''' <returns>True, if the specified values are identical within the specified precision.</returns>
    ''' 
    Private Shared Function CompareNullableDoubles(value1 As Double?, value2 As Double?, formatStr As String) As Boolean

        Dim res1 As Double?
        Dim res2 As Double?

        If value1 IsNot Nothing Then
            Dim resStr = Format(value1, formatStr)
            res1 = CDbl(resStr)
        End If

        If value2 IsNot Nothing Then
            Dim resStr = Format(value2, formatStr)
            res2 = CDbl(resStr)
        End If

        Return res1.Equals(res2)

    End Function

End Class


Public Class ProtocolTypeSelector

    Inherits DataTemplateSelector

    Public Overrides Function SelectTemplate(item As Object, container As DependencyObject) As DataTemplate

        Dim protocolEntry As tblProtocolItems = item
        Dim element As FrameworkElement = container

        If protocolEntry Is Nothing OrElse element Is Nothing Then
            Return Nothing
        End If

        Select Case protocolEntry.ElementType

            Case ProtocolElementType.RefReactant
                Return element.TryFindResource("RefReactantItem")

            Case ProtocolElementType.Reagent
                Return element.TryFindResource("ReagentItem")

            Case ProtocolElementType.Solvent
                Return element.TryFindResource("SolventItem")

            Case ProtocolElementType.Auxiliary
                Return element.TryFindResource("AuxiliaryItem")

            Case ProtocolElementType.Product
                Return element.TryFindResource("ProductItem")

            Case ProtocolElementType.Comment
                Return element.TryFindResource("CommentItem")

            Case ProtocolElementType.File
                Return element.TryFindResource("FileItem")

            Case ProtocolElementType.Image
                Return element.TryFindResource("ImageItem")

            Case ProtocolElementType.Separator
                Return element.TryFindResource("SeparatorItem")

            Case Else
                Return Nothing

        End Select

    End Function


End Class










