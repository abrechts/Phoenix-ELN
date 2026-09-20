

Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Partial Public Class ExpTreeHeader

    Public Shared Event RequestUpdateWorkflowState(sender As Object, targetExp As tblExperiments, requestedState As WorkflowStatus)


    Public Sub New()

        ' This call is required by the Windows Form Designer.
        InitializeComponent()

        ' Reaction sketch tooltip: only an empty shell here, its content is built on hover in Me_ToolTipOpening,
        ' so building the tree costs nothing.
        ToolTip = New ToolTip With {.IsHitTestVisible = False, .Placement = Primitives.PlacementMode.Right,
            .Style = CType(FindResource("sketchToolTipStyle"), Style)}
        ' Placed relative to the header's content, not to the control: in stretched lists (search results) the control
        ' is as wide as the list item, which would push the tooltip far to the right of the experiment text.
        ToolTipService.SetPlacementTarget(Me, headerContent)
        ToolTipService.SetInitialShowDelay(Me, 500)
        ToolTipService.SetShowDuration(Me, 15000)

    End Sub


    ''' <summary>
    ''' Gets or sets if the reaction sketch tooltip should be suppressed (default false).
    ''' </summary>
    '''
    Public Property SuppressSketchToolTip As Boolean = False


    ''' <summary>
    ''' Inherited attached property to switch the reaction sketch tooltip on/off for all ExpTreeHeaders below an element
    ''' (default true). Set on the experiment tree so its toolbar toggle covers every header, without per-item bindings.
    ''' </summary>
    '''
    Public Shared ReadOnly SketchToolTipsEnabledProperty As DependencyProperty =
        DependencyProperty.RegisterAttached("SketchToolTipsEnabled", GetType(Boolean), GetType(ExpTreeHeader),
            New FrameworkPropertyMetadata(True, FrameworkPropertyMetadataOptions.Inherits))

    Public Shared Function GetSketchToolTipsEnabled(obj As DependencyObject) As Boolean
        Return CBool(obj.GetValue(SketchToolTipsEnabledProperty))
    End Function

    Public Shared Sub SetSketchToolTipsEnabled(obj As DependencyObject, value As Boolean)
        obj.SetValue(SketchToolTipsEnabledProperty, value)
    End Sub

    Private Const TipMaxWidth As Double = 450
    Private Const TipMaxHeight As Double = 260

    ' Display size relative to the canvas' native size. The same scale for every sketch keeps bond lengths
    ' consistent; the max box only kicks in for exceptionally wide/tall sketches, which then shrink further.
    '
    Private Const TipScale As Double = 0.2


    ' The sketch canvas is built fresh on every hover: a canvas can only have one visual parent, and parsing
    ' takes well under 20 ms, so caching isn't worth it.
    '
    Private Sub Me_ToolTipOpening(sender As Object, e As ToolTipEventArgs) Handles Me.ToolTipOpening

        Dim tip = TryCast(ToolTip, ToolTip)
        Dim expEntry = TryCast(DataContext, tblExperiments)

        If tip Is Nothing OrElse SuppressSketchToolTip OrElse Not GetSketchToolTipsEnabled(Me) OrElse expEntry Is Nothing OrElse String.IsNullOrEmpty(expEntry.RxnSketch) Then
            e.Handled = True    'no tooltip
            Return
        End If

        Dim skInfo = DrawingEditor.GetSketchInfo(expEntry.RxnSketch)

        If skInfo Is Nothing OrElse skInfo.SketchCanvas Is Nothing Then
            e.Handled = True
            Return
        End If

        ' Size the viewbox proportionally to the canvas' native size (so small structures stay small), capped at the
        ' max box while keeping the aspect ratio. Falls back to the max box if the canvas has no explicit size.
        '
        Dim cvs = skInfo.SketchCanvas
        Dim boxW = TipMaxWidth
        Dim boxH = TipMaxHeight

        If cvs.Width > 0 AndAlso cvs.Height > 0 Then     'false for NaN
            Dim s = Math.Min(1, Math.Min(TipMaxWidth / (cvs.Width * TipScale), TipMaxHeight / (cvs.Height * TipScale))) * TipScale
            boxW = cvs.Width * s
            boxH = cvs.Height * s
        End If

        Dim box As New Viewbox With {.Child = cvs, .Stretch = Media.Stretch.Uniform, .Width = boxW, .Height = boxH}
        tip.Content = box

    End Sub


    ''' <summary>
    ''' Sets or gets if the indicator icon for pinned experiments (right arrow) should be displayed (the default is true). 
    ''' </summary>
    ''' 
    Public Property IsTabIndicatorVisible As Boolean
        Get
            Return icoTabOpen.IsVisible
        End Get
        Set(value As Boolean)
            icoTabOpen.Visibility = If(value, Visibility.Visible, Visibility.Collapsed)
        End Set
    End Property


    ''' <summary>
    ''' Gets or sets if the context menu should be suppressed. Typically utilized for use of the experiments 
    ''' header other than in the experiments tree, e.g. in search result lists.
    ''' </summary>
    ''' 
    Public Property SuppressContextMenu As Boolean = False


    Private Sub Me_ContextMenuOpening(sender As Object, e As RoutedEventArgs) Handles Me.ContextMenuOpening

        If SuppressContextMenu Then
            e.Handled = True
        End If

    End Sub

    Private Sub Me_PreviewMouseRightButtonUp(sender As Object, e As RoutedEventArgs) Handles Me.PreviewMouseRightButtonUp

        Dim tvItem = WPFToolbox.FindVisualParent(Of TreeViewItem)(Me)
        If tvItem IsNot Nothing Then
            tvItem.IsSelected = True
        End If

    End Sub


    Private Sub mnuFinalize_Click() Handles mnuFinalize.Click

        Dim expEntry = CType(DataContext, tblExperiments)
        RaiseEvent RequestUpdateWorkflowState(Me, expEntry, WorkflowStatus.Finalized)

    End Sub


    Private Sub mnuUnlock_Click() Handles mnuUnlock.Click

        Dim expEntry = CType(DataContext, tblExperiments)
        RaiseEvent RequestUpdateWorkflowState(Me, expEntry, WorkflowStatus.Unlocked)

    End Sub

End Class


Public Class ExpStateToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim workflowState As WorkflowStatus = value

        If LCase(parameter) = "invert" Then
            Return If(workflowState <> WorkflowStatus.Finalized, Visibility.Visible, Visibility.Collapsed)
        Else
            Return If(workflowState = WorkflowStatus.Finalized, Visibility.Visible, Visibility.Collapsed)
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class PinnedToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim displayIndex As Integer = value

        Select Case displayIndex
            Case -1, > 0
                Return Visibility.Visible
            Case Else
                Return Visibility.Hidden
        End Select

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
