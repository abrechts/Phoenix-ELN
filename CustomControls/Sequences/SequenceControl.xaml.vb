
Imports System.Windows
Imports System.Windows.Controls
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
    ''' 
    Public Property SequenceTitle As String
        Get
            Return blkMainTitle.Text
        End Get
        Set(value As String)
            blkMainTitle.Text = value
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the InChIKey of the reference reactant at the start of the multi-step synthetic reaction sequence. 
    ''' </summary>
    ''' 
    Public Property StartInChIKey As String


    ''' <summary>
    ''' Sets or gets the InChIKey of the reference product at the end of the multi-step synthetic reaction sequence. 
    ''' </summary>
    ''' 
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
    Public Property DownstreamSequences As New List(Of SequenceControl)


    ''' <summary>
    ''' Sets or gets the list of all directly connected upstream sequences.
    ''' </summary>
    ''' <returns></returns>
    Public Property UpstreamSequences As New List(Of SequenceControl)


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

        Dim firstStep As New SequenceStep(initialExp.ReactantInChIKey, initialExp.ProductInChIKey, dbContext)
        SequenceSteps.Add(firstStep)

        AddDownstreamElements(firstStep)
        AddUpstreamElements(firstStep)

        SequenceTitle = StartInChIKey.Substring(0, 4) + "/" + EndInChIKey.Substring(0, 4)

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
        SequenceSteps.Add(connectingStep)

        If direction = SequenceDirection.Downstream Then
            AddDownstreamElements(connectingStep)
        Else
            AddUpstreamElements(connectingStep)
        End If

        SequenceTitle = StartInChIKey.Substring(0, 4) + "/" + EndInChIKey.Substring(0, 4)

    End Sub


    ''' <summary>
    ''' Recursively adds downstream steps and sequences relative to the specified step
    ''' </summary>
    ''' 
    Private Sub AddDownstreamElements(refStep As SequenceStep)

        '    Dim downStreamConnectSequences As New List(Of SequenceControl)
        Dim endReached As Boolean = False

        While Not endReached

            'Otherwise get next connects
            Dim nextConnects = refStep.GetNextSteps

            Select Case nextConnects.Count

                Case 0 'no more connecting steps, end of sequence

                    endReached = True

                Case 1  'sequence continues -> add next step

                    refStep = nextConnects.First

                    'end sequence if refStep has multiple incoming sequences (branch-in situation, with common product of multiple sequences) 
                    Dim prevConnects = refStep.GetPreviousSteps
                    If prevConnects.Count > 1 Then
                        endReached = True
                        Exit While
                    End If

                    SequenceSteps.Add(refStep)
                    EndInChIKey = refStep.ProductInChIKey

                Case > 1 'multiple downstream connects -> branch off

                    VerticalConnectorRight.Visibility = Visibility.Visible

                    'create (recursive) downstream elements 
                    For Each connStep In nextConnects
                        Dim downSequence As New SequenceControl(connStep, SequenceDirection.Downstream) 'recursive
                        downSequence.ShowUpstreamConnector()
                        DownstreamSequences.Add(downSequence)   'add to list, not UI
                    Next

                    'detect converging downstream sequences
                    Dim res = From seq In DownstreamSequences Group By seq.EndInChIKey Into convergentGroups = Group

                    For Each result In res

                        Dim downStrElement As New AdjacentSequenceItem

                        If result.convergentGroups.Count = 1 Then

                            'no convergence -> add next connecting sequence to downStream panel
                            Dim seq = result.convergentGroups.First
                            downStrElement.pnlConvSequences.Children.Add(seq)

                        Else

                            'convergent sequences -> add converging sequence group

                            For Each seq In result.convergentGroups
                                seq.ShowDownstreamConnector()
                                downStrElement.pnlConvSequences.Children.Add(seq)
                            Next

                            Dim finalStep = result.convergentGroups.First.SequenceSteps.Last
                            If finalStep.GetNextSteps.Count > 0 Then

                                'merging to a downstream sequence

                                Dim connStep = finalStep.GetNextSteps.First
                                Dim convergedSequence As New SequenceControl(connStep, SequenceDirection.Downstream) 'recurs
                                With downStrElement
                                    .pnlConvergingDown.Visibility = Visibility.Visible
                                    .pnlConvergingDown.Children.Add(convergedSequence)
                                End With

                            Else

                                'merging to common product without further sequence

                                With downStrElement
                                    .btnMergeRightCenter.Visibility = Visibility.Visible
                                    .pnlConvergingDown.Visibility = Visibility.Visible
                                End With

                            End If
                        End If

                        pnlDownstream.Children.Add(downStrElement)

                    Next

                    endReached = True

                    ShowDownstreamConnector()

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
            Dim nextConnects = refStep.GetPreviousSteps
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


    Public Sub ShowUpstreamConnector()

        upConnector.Visibility = Visibility.Visible

    End Sub


    Public Sub ShowDownstreamConnector()

        downConnector.Visibility = Visibility.Visible

    End Sub


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
    Public Function GetNextSteps() As List(Of SequenceStep)

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
    Public Function GetPreviousSteps() As List(Of SequenceStep)

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









