
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Documents
Imports System.Windows.Media


Public Class SequenceControl

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' 
    ''' </summary>
    ''' 
    Public Sub AddDownstreamSequence(isConvergentGroup As Boolean)

        Dim downStreamItem As New DownStreamElement


    End Sub


    Public Sub AddUpstreamSequence()


    End Sub

    '''' <summary>
    '''' Sets or gets the SequenceBounding info of the main scheme control
    '''' </summary>
    '''' 
    'Public Property MainSchemeInfo As SequenceBoundingResult

    '    Get
    '        Return mMainSchemeInfo
    '    End Get

    '    Set(value As ExpEntityData.SequenceBoundingResult)

    '        DataContext = value.SchemeEntry
    '        mMainSchemeInfo = value
    '        If IsNothing(mMainSchemeInfo.SchemeEntry) Then
    '            'this is an *intermediate*
    '            With btnMain
    '                .Content = "C"
    '                .IsEnabled = False
    '                .ToolTip = "Common intermediate/product"
    '                .Width = 18
    '            End With
    '        End If

    '        'highlight current element
    '        If Not IsNothing(FlowSchemeControl.InitialSchemeEntry) AndAlso Not IsNothing(MainSchemeInfo.SchemeEntry) Then
    '            If MainSchemeInfo.SchemeEntry.ID = FlowSchemeControl.InitialSchemeEntry.ID Then
    '                btnMain.IsChecked = True
    '                FlowSchemeControl.LastActiveElement = Me
    '                FlowSchemeControl.InitialSchemeEntry = Nothing
    '            End If
    '        End If

    '    End Set

    'End Property



    ''' <summary>
    ''' Displays tooltip whenever content of blkMainTitle is shortened by ellipsis character
    ''' </summary>
    ''' 
    Private Sub blkMainTitle_SizeChanged() Handles blkMainTitle.SizeChanged

        blkMainTitle.Measure(New Size(Double.PositiveInfinity, Double.PositiveInfinity))
        Dim blkFullWidth = blkMainTitle.DesiredSize.Width

        If blkMainTitle.ActualWidth < blkFullWidth Then
            btnMain.ToolTip = blkMainTitle.Text
        End If

    End Sub


    Private Sub btnMain_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnMain.Click

        'If Not IsNothing(MainSchemeInfo.SchemeEntry) Then
        '    btnMain.IsChecked = True
        '    '      RaiseEvent SequenceLinkClicked(Me, MainSchemeInfo.SchemeEntry)
        'Else
        '    UncheckControl()
        'End If

    End Sub


    ''' <summary>
    ''' Typically used for unchecking previous button when another one is checked
    ''' </summary>
    '''
    Public Sub UncheckControl()

        btnMain.IsChecked = False

    End Sub


    Public Sub CheckControl()

        btnMain.IsChecked = True

    End Sub



    '''' <summary>
    '''' Adds a sequence to the vertical upstream panel
    '''' </summary>
    '''' <param name="sequence">The sequence to be added upstream</param>
    '''' <remarks></remarks>
    '''' 
    'Public Sub AddUpstreamSequence(sequence As SequenceBoundingResult)

    '    Dim upstrCtrl As New SequenceControl
    '    With upstrCtrl
    '        .HorizontalAlignment = Windows.HorizontalAlignment.Right
    '        .Margin = New Thickness(0, 1, 0, 1)
    '        .MainSchemeInfo = sequence
    '    End With
    '    pnlUpstream.Children.Add(upstrCtrl)
    '    FlowSchemeControl.FlowSchemeElements.Add(upstrCtrl)

    '    'update adorners
    '    UpdateLayout()
    '    Dim myAdornerLayer As AdornerLayer = AdornerLayer.GetAdornerLayer(Me)
    '    If Not IsNothing(myAdornerLayer) Then
    '        Dim upstreamConnector = New FlowSchemeUpstreamAdorners(pnlUpstream)
    '        myAdornerLayer.Add(upstreamConnector)
    '    End If

    'End Sub


    '''' <summary>
    '''' Populates the downstream panel of the control.
    '''' </summary>
    '''' <param name="sequences">List containing sequences with identical end points (convergent), or a 
    '''' single sequence (non-convergent).</param>
    '''' <param name="convergingSequence">Converging sequence if applicable, otherwise nothing.</param>
    '''' <returns>Newly create downstream SequenceControl control</returns>
    '''' 
    'Public Function AddDownstreamSequences(sequences As List(Of SequenceBoundingResult), Optional convergingSequence As SequenceBoundingResult = Nothing) As SequenceControl

    '    Dim downStrCtrl As New FlowSchemeDownStrElement

    '    Dim newCtrl = downStrCtrl.Populate(sequences, convergingSequence)
    '    pnlDownstream.Children.Add(downStrCtrl)
    '    downStrCtrl.SetConnectors()

    '    'update adorners
    '    UpdateLayout() 'important, otherwise adornerlayer sometimes nothing
    '    Dim myAdornerLayer As AdornerLayer = AdornerLayer.GetAdornerLayer(Me)
    '    If Not IsNothing(myAdornerLayer) Then
    '        Dim downstreamConnector = New FlowSchemeDownstreamAdorners(Me)
    '        myAdornerLayer.Add(downstreamConnector)
    '    End If

    '    Return newCtrl

    'End Function


    '--------------
    ' Adorners
    '--------------

    Public Class FlowSchemeUpstreamAdorners

        Inherits Adorner

        Dim mUpstreamPanel As StackPanel
        Dim mDownstreamPanel As StackPanel

        Public Enum DrawDirection
            Left
            Right
            BothSides
        End Enum

        Public Sub New(ByVal adornedElement As UIElement)

            MyBase.New(adornedElement)
            mUpstreamPanel = adornedElement

        End Sub


        Protected Overrides Sub OnRender(ByVal drawingContext As DrawingContext)

            Dim adornedElementRect = New Rect(AdornedElement.DesiredSize)
            Dim penWidth As Double = 1.5
            Dim horizOffset As Double = 8
            Dim arrowLength As Double = 15
            Dim ptTop As Point
            Dim ptBottom As Point
            Dim ptMiddleStart As Point
            Dim ptMiddleEnd As Point

            MyBase.OnRender(drawingContext)

            Dim renderPen = New Pen(New SolidColorBrush(Colors.BlueViolet), penWidth)
            Dim topUpstrRect = LayoutInformation.GetLayoutSlot(mUpstreamPanel.Children(0))
            Dim bottomUpstrRect = LayoutInformation.GetLayoutSlot(mUpstreamPanel.Children.Item(mUpstreamPanel.Children.Count - 1))

            'draw vertical connectors if several elements present (should be the case by definition)
            If mUpstreamPanel.Children.Count > 1 Then
                ptTop = topUpstrRect.TopRight
                ptBottom = bottomUpstrRect.TopRight
                ptTop.Offset(horizOffset, topUpstrRect.Height / 2)
                ptBottom.Offset(horizOffset, bottomUpstrRect.Height / 2)
                drawingContext.DrawLine(renderPen, ptTop, ptBottom) 'draw to context
            End If

            'draw horizontal upstream connectors to all vertical panel children
            For Each mychild In mUpstreamPanel.Children
                Dim childRect = LayoutInformation.GetLayoutSlot(mychild)
                Dim yPos = childRect.Top + childRect.Height / 2
                drawingContext.DrawLine(renderPen, New Point(childRect.Right - 3, yPos), New Point(childRect.Right + horizOffset, yPos))
            Next

            'draw downstream arrow
            ptMiddleStart = New Point(ptTop.X, ptTop.Y + (ptBottom.Y - ptTop.Y) / 2)
            ptMiddleEnd = New Point(ptTop.X + arrowLength, ptMiddleStart.Y)
            drawingContext.DrawGeometry(Brushes.BlueViolet, renderPen, WPFToolbox.CreateArrow(ptMiddleStart, ptMiddleEnd, 4, 4))    'arrow

        End Sub

    End Class


    Public Class FlowSchemeDownstreamAdorners

        Inherits Adorner

        Dim mDirection As DrawDirection
        Dim mFlowElement As SequenceControl
        Dim mDownstreamPanel As StackPanel

        Public Enum DrawDirection
            Left
            Right
            BothSides
        End Enum

        Public Sub New(ByVal adornedElement As UIElement)

            MyBase.New(adornedElement)
            mFlowElement = adornedElement

        End Sub


        Protected Overrides Sub OnRender(ByVal drawingContext As DrawingContext)

            Dim adornedElementRect = New Rect(AdornedElement.DesiredSize)
            Dim penWidth As Double = 1.5
            Dim horizOffset As Double = 4
            Dim arrowLength As Double = 15

            Dim downstreamPanel = mFlowElement.pnlDownstream
            Dim ptTop As Point
            Dim ptBottom As Point
            Dim ptMiddle As Point

            MyBase.OnRender(drawingContext)

            Dim renderPen = New Pen(New SolidColorBrush(Colors.BlueViolet), penWidth)
            Dim downstrRect = LayoutInformation.GetLayoutSlot(downstreamPanel)

            ''draw downstream vertical connector
            'With downstreamPanel.Children
            '    If .Count > 0 Then
            '        'get top connector pos
            '        Dim ptOrigTop = CType(.Item(0), FlowSchemeDownStrElement).TopLeftConnectPt
            '        Dim ptOrigBottom = CType(.Item(.Count - 1), FlowSchemeDownStrElement).BottomLeftConnectPt
            '        With downstrRect
            '            ptTop = New Point(.Left + ptOrigTop.X + horizOffset, .Top + ptOrigTop.Y)
            '            If downstreamPanel.Children.Count() > 1 Then
            '                ptBottom = New Point(.Left + ptOrigTop.X + horizOffset, .Bottom - ptOrigBottom.Y)
            '            Else
            '                ptBottom = Point.Add(ptTop, New Point(0, (ptOrigBottom.Y - ptOrigTop.Y)))
            '            End If
            '        End With
            '        drawingContext.DrawLine(renderPen, ptTop, ptBottom)
            '    End If
            'End With

            'draw downstream arrow
            Dim mainRect = LayoutInformation.GetLayoutSlot(mFlowElement.pnlMain)
            With mainRect
                ptMiddle = New Point(.Right + 2, .Top + .Height / 2)
            End With
            drawingContext.DrawLine(renderPen, ptMiddle, New Point(ptTop.X, ptMiddle.Y))

        End Sub


    End Class



End Class
