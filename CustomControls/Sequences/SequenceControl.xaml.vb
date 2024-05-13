
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Documents
Imports System.Windows.Media
Imports ElnBase
Imports ElnCoreModel


Public Class SequenceControl


    Public Enum SequenceDirection
        Downstream
        Upstream
    End Enum


    ''' <summary>
    ''' Sets or gets the title of the sequence
    ''' </summary>
    Public Property SequenceTitle As String


    ''' <summary>
    ''' Sets or gets the InChIKey of the reference reactant at the start of the multi-step synthetic reaction sequence. 
    ''' </summary>
    Public Property StartInChIKey As String


    ''' <summary>
    ''' Sets or gets the InChIKey of the reference product at the end of the multi-step synthetic reaction sequence. 
    ''' </summary>
    Public Property EndInChIKey As String


    ''' <summary>
    ''' Sets or gets a list of the synthetic steps the ReactionSequence consists of, ordered by their position in the sequence.
    ''' </summary>
    ''' 
    Public Property SequenceSteps As New List(Of SequenceStep)


    ''' <summary>
    ''' Sets or gets a list of all directly connected downstream sequences.
    ''' </summary>
    ''' 
    Public Property DownstreamSequences As List(Of SequenceControl)


    ''' <summary>
    ''' Sets or gets the list of all directly connected upstream sequences.
    ''' </summary>
    ''' <returns></returns>
    Public Property UpstreamSequences As List(Of SequenceControl)


    ''' <summary>
    ''' For XAML designer only
    ''' </summary>
    ''' 
    Public Sub New()

        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Creates a new reaction sequence based on the specified experiment entry, then recursively adds all downstream and 
    ''' upstream steps. Additional sequences are recursively added on the way.
    ''' </summary>
    ''' <param name="initialExp">The seed experiment to build the sequence around.</param>
    ''' <param name="dbContext">The database context (local or server based).</param>
    ''' 
    Public Sub New(initialExp As tblExperiments, dbContext As ElnDbContext)

        InitializeComponent()

        StartInChIKey = initialExp.ReactantInChIKey
        EndInChIKey = initialExp.ProductInChIKey
        SequenceTitle = "Seed Sequence"

        Dim firstStep As New SequenceStep(initialExp.ReactantInChIKey, initialExp.ProductInChIKey, dbContext)
        SequenceSteps.Add(firstStep)

        AddDownstreamElements(firstStep)
        AddUpstreamElements(firstStep)

    End Sub


    ''' <summary>
    ''' Creates a new reaction sequence based on the specified step and direction, then recursively adds all downstream and 
    ''' upstream steps and additional sequences.
    ''' </summary>
    ''' <param name="connectingStep">The first or last step (defined by the SequenceDirection parameter) of a sequence to connect to.</param>
    ''' <param name="direction">The direction of the connection (upstream or downstream).</param>
    ''' 
    Friend Sub New(connectingStep As SequenceStep, direction As SequenceDirection)

        InitializeComponent()

        StartInChIKey = connectingStep.ReactantInChIKey
        EndInChIKey = connectingStep.ProductInChIKey
        SequenceTitle = "Sequence"
        SequenceSteps.Add(connectingStep)

        If direction = SequenceDirection.Downstream Then
            AddDownstreamElements(connectingStep)
        Else
            AddUpstreamElements(connectingStep)
        End If

    End Sub


    ''' <summary>
    ''' Recursively adds downstream steps and sequences relative to the specified step
    ''' </summary>
    ''' 
    Private Sub AddDownstreamElements(refStep As SequenceStep)

        Dim downStreamConnectSequences As New List(Of SequenceControl)
        Dim endReached As Boolean = False

        While Not endReached

            Dim nextConnects = refStep.GetNextStep

            Select Case nextConnects.Count

                Case 0 'no more connecting steps, end of sequence

                    endReached = True

                Case 1  'sequence continues -> add next step

                    refStep = nextConnects.First
                    SequenceSteps.Add(refStep)
                    EndInChIKey = refStep.ProductInChIKey

                Case > 1 'multiple downstream connects -> branch off

                    'create (recursive) downstream elements 
                    For Each connStep In nextConnects
                        Dim downSequence As New SequenceControl(connStep, SequenceDirection.Downstream) 'recursive
                        downStreamConnectSequences.Add(downSequence)
                    Next

                    'detect converging downstream sequences
                    Dim res = From seq In downStreamConnectSequences Group By seq.EndInChIKey Into convergentGroups = Group
                    For Each result In res

                        Dim downStrElement As New AdjacentSequenceItem

                        If result.convergentGroups.Count = 1 Then

                            'no convergence -> add next connecting sequence to downStream panel
                            downStrElement.pnlConvSequences.Children.Add(result.convergentGroups.First)

                        Else

                            'convergent sequences -> add converging sequence group

                            For Each seq In result.convergentGroups
                                downStrElement.pnlConvSequences.Children.Add(seq)
                                'test
                                downStrElement.pnlConvSequences.Background = Brushes.WhiteSmoke
                            Next

                            '-----------------------------------------------------------------
                            'TODO: Add converging element to downStrElement.pnlConvergingDown
                            '-----------------------------------------------------------------

                        End If

                        pnlDownstream.Children.Add(downStrElement)

                    Next

                    endReached = True

            End Select

        End While

    End Sub


    ''' <summary>
    ''' Recursively adds upstream steps and sequences relative to the specified step
    ''' </summary>
    ''' 
    Private Sub AddUpstreamElements(refStep As SequenceStep)

        Dim endReached As Boolean = False

        While Not endReached
            Dim nextConnects = refStep.GetPreviousStep
            If nextConnects.Count = 1 Then
                refStep = nextConnects.First
                SequenceSteps.Insert(0, refStep)
                StartInChIKey = refStep.ReactantInChIKey
            Else
                If nextConnects.Count > 1 Then
                    'branch off upstream
                    For Each connStep In nextConnects
                        Dim upSequence As New SequenceControl(connStep, SequenceDirection.Upstream)
                        UpstreamSequences.Add(upSequence)
                    Next
                End If

                'TODO: detect groups of converging upstream sequences (each group having identical start InChIKey)

                endReached = True 'start of sequence: no more steps, or branching off
            End If
        End While

    End Sub

    '' <summary>
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


End Class


Public Class SequenceStep

    Public Sub New(reactInChIKey As String, prodInChIKey As String, dbContext As ElnDbContext)

        ReactantInChIKey = reactInChIKey
        ProductInChIKey = prodInChIKey
        DatabaseContext = dbContext

    End Sub

    Public Property DatabaseContext As ElnDbContext

    Public Property ReactantInChIKey As String

    Public Property ProductInChIKey As String


    ''' <summary>
    ''' Gets a list of all experiments contained in this step.
    ''' </summary>
    ''' 
    Public ReadOnly Property GetStepExperiments As IEnumerable(Of tblExperiments)
        Get
            Return From exp In DatabaseContext.tblExperiments Where exp.ReactantInChIKey = ReactantInChIKey AndAlso exp.ProductInChIKey = ProductInChIKey
        End Get
    End Property


    ''' <summary>
    ''' Gets the next downstream connecting step(s) based on the current product InChIKey. If an empty list is returned, then 
    ''' the end of the sequence is reached. If more than one connecting step is returned, then the sequence is to be completed due to 
    ''' sequence branching.
    ''' </summary>
    ''' 
    Public Function GetNextStep() As List(Of SequenceStep)

        Dim nextStepInChIList = (From exp In DatabaseContext.tblExperiments Where exp.ReactantInChIKey = ProductInChIKey
                                 Select exp.ProductInChIKey).Distinct

        Dim res As New List(Of SequenceStep)

        For Each nextStepProdInchI In nextStepInChIList
            Dim newStep As New SequenceStep(ProductInChIKey, nextStepProdInchI, DatabaseContext)
            res.Add(newStep)
        Next

        Return res

    End Function


    ''' <summary>
    ''' Gets the previous upstream connecting step(s) based on the current reactant InChIKey. If an empty list is returned, then 
    ''' the end of the sequence is reached. If more than one connecting step is returned, then the sequence is to be completed 
    ''' due to sequence branching.
    ''' </summary>
    ''' 
    Public Function GetPreviousStep() As List(Of SequenceStep)

        Dim prevStepInChIList = (From exp In DatabaseContext.tblExperiments Where exp.ProductInChIKey = ReactantInChIKey
                                 Select exp.ReactantInChIKey).Distinct

        Dim res As New List(Of SequenceStep)

        For Each prevStepInChI In prevStepInChIList
            Dim newStep As New SequenceStep(prevStepInChI, ReactantInChIKey, DatabaseContext)
            res.Add(newStep)
        Next

        Return res

    End Function


    ''' <summary>
    ''' Gets the reference reactant image as canvas
    ''' </summary>
    ''' 
    Public Function GetReactantImage() As Canvas

        Dim firstExpRxnSketch = GetStepExperiments.First.RxnSketch
        Dim skInfo = DrawingEditor.GetSketchInfo(firstExpRxnSketch)
        Return skInfo.Reactants.First.StructureCanvas

    End Function


    ''' <summary>
    ''' Gets the reference product image as canvas
    ''' </summary>
    ''' 
    Public Function GetProductImage() As Canvas

        Dim firstExpRxnSketch = GetStepExperiments.First.RxnSketch
        Dim skInfo = DrawingEditor.GetSketchInfo(firstExpRxnSketch)
        Return skInfo.Products.First.StructureCanvas

    End Function


End Class




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



'''' <summary>
'''' Displays tooltip whenever content of blkMainTitle is shortened by ellipsis character
'''' </summary>
'''' 
'Private Sub blkMainTitle_SizeChanged() Handles blkMainTitle.SizeChanged

'    blkMainTitle.Measure(New Size(Double.PositiveInfinity, Double.PositiveInfinity))
'    Dim blkFullWidth = blkMainTitle.DesiredSize.Width

'    If blkMainTitle.ActualWidth < blkFullWidth Then
'        btnMain.ToolTip = blkMainTitle.Text
'    End If

'End Sub


'Private Sub btnMain_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnMain.Click

'    'If Not IsNothing(MainSchemeInfo.SchemeEntry) Then
'    '    btnMain.IsChecked = True
'    '    '      RaiseEvent SequenceLinkClicked(Me, MainSchemeInfo.SchemeEntry)
'    'Else
'    '    UncheckControl()
'    'End If

'End Sub


'''' <summary>
'''' Typically used for unchecking previous button when another one is checked
'''' </summary>
''''
'Public Sub UncheckControl()

'    btnMain.IsChecked = False

'End Sub


'Public Sub CheckControl()

'    btnMain.IsChecked = True

'End Sub



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

