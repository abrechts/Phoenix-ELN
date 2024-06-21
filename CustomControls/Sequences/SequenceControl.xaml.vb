
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
    ''' Sets of gets the step from which the sequence build was initiated. Is nothing for all non-seed sequences.
    ''' </summary>
    ''' 
    Public Property SeedStep As SequenceStep


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
    ''' Sets or gets if the sequence has downstream children
    ''' </summary>
    ''' 
    Public Property HasDownstreamConnections As Boolean = False


    ''' <summary>
    ''' Sets or gets if the sequence has an upstream parent
    ''' </summary>
    ''' 
    Public Property HasUpstreamConnections As Boolean = False


    ''' <summary>
    ''' Occurs when this control is clicked
    ''' </summary>
    ''' 
    Public Shared Event SequenceSelected(sender As Object)


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

        DownstreamSequenceNr = 1
        UpstreamSequenceNr = 0

        Dim firstStep As New SequenceStep(initialExp.ReactantInChIKey, initialExp.ProductInChIKey, dbContext)
        firstStep.IsSeedStep = True
        SequenceSteps.Add(firstStep)
        SeedStep = firstStep

        AddDownstreamElements(firstStep)
        AddUpstreamElements(firstStep)

        SequenceTitle = "Main Sequence"

        UpdateLayout()

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

        If Not SequenceSteps.Contains(connectingStep) Then
            SequenceSteps.Add(connectingStep)
        End If

        If direction = SequenceDirection.Downstream Then
            AddDownstreamElements(connectingStep)
        Else
            AddUpstreamElements(connectingStep)
        End If

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        AddHandler dlgSequences.ClearSequenceSelections, AddressOf dlgSequences_ClearSequenceSelections

    End Sub


    Public Shared UpstreamSequenceNr As Integer = 0

    Public Shared DownstreamSequenceNr As Integer = 0


    ''' <summary>
    ''' Recursively adds downstream steps and sequences relative to the specified step
    ''' </summary>
    ''' 
    Private Sub AddDownstreamElements(refStep As SequenceStep)

        Dim downstreamSequences As New List(Of SequenceControl)
        Dim endReached As Boolean = False

        While Not endReached

            'Otherwise get next connects
            Dim nextConnects = refStep.GetNextSteps

            Select Case nextConnects.Count

                Case 0 'no more connecting steps, end of sequence

                    endReached = True

                Case 1  'sequence continues -> add next step, if not upstream converging one

                    refStep = nextConnects.First

                    'complete sequence if refStep has multiple incoming sequences and does not contain seed step
                    Dim prevConnects = refStep.GetPreviousSteps
                    If prevConnects.Count > 1 AndAlso Not SequenceSteps.Contains(SeedStep) Then
                        endReached = True
                        Exit While
                    End If

                    SequenceSteps.Add(refStep)
                    EndInChIKey = refStep.ProductInChIKey

                Case > 1 'multiple downstream connects -> branch off

                    HasDownstreamConnections = True
                    VerticalConnectorRight.Visibility = Visibility.Visible

                    'create (recursive) downstream elements 
                    For Each connStep In nextConnects
                        Dim downSequence As New SequenceControl(connStep, SequenceDirection.Downstream) 'recursive
                        downSequence.ShowUpstreamConnector()
                        downSequence.HasUpstreamConnections = True
                        DownstreamSequences.Add(downSequence)   'add to list, not UI
                    Next

                    'detect converging downstream sequences
                    Dim res = From seq In downstreamSequences Group By seq.EndInChIKey Into convergentGroups = Group

                    For Each result In res

                        Dim downStrElement As New AdjacentSequenceItem

                        If result.convergentGroups.Count = 1 Then

                            'no convergence -> add next connecting sequence to downStream panel
                            Dim seq = result.convergentGroups.First
                            downStrElement.pnlConvSequences.Children.Add(seq)
                            DownstreamSequenceNr += 1
                            seq.SequenceTitle = "Sequence " + DownstreamSequenceNr.ToString

                        Else

                            'convergent sequences -> add converging sequence group

                            DownstreamSequenceNr += 1

                            Dim pos As Integer = 0
                            For Each seq In result.convergentGroups
                                seq.ShowDownstreamConnector()
                                seq.HasDownstreamConnections = True
                                seq.SequenceTitle = "Sequence " + DownstreamSequenceNr.ToString + NumberToCharacter(pos)
                                pos += 1
                                downStrElement.pnlConvSequences.Children.Add(seq)
                            Next

                            Dim finalStep = result.convergentGroups.First.SequenceSteps.Last
                            If finalStep.GetNextSteps.Count > 0 Then

                                'merging to a downstream sequence

                                Dim connStep = finalStep.GetNextSteps.First
                                Dim convergedSequence As New SequenceControl(connStep, SequenceDirection.Downstream) 'recursive
                                With convergedSequence
                                    DownstreamSequenceNr += 1
                                    .HasUpstreamConnections = True
                                    .SequenceTitle = "Sequence " + DownstreamSequenceNr.ToString
                                End With

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
    ''' Converts 1 to 'a', 2 to 'b', etc.
    ''' </summary>
    ''' 
    Private Function NumberToCharacter(val As Integer) As String

        Return ChrW((AscW("a"c) + val))

    End Function


    ''' <summary>
    ''' Recursively adds upstream steps and sequences relative to the specified step
    ''' </summary>
    ''' 
    Private Sub AddUpstreamElements(refStep As SequenceStep)

        Dim upstreamSequences As New List(Of SequenceControl)
        Dim endReached As Boolean = False

        While Not endReached

            'Otherwise get next connects
            Dim prevConnects = refStep.GetPreviousSteps

            Select Case prevConnects.Count

                Case 0 'no more connecting steps, end of sequence

                    endReached = True

                Case 1  'sequence continues -> add previous step

                    refStep = prevConnects.First

                    'complete sequence if refStep has multiple incoming sequences and does not contain seed step
                    Dim nextConnects = refStep.GetNextSteps
                    If nextConnects.Count > 1 AndAlso Not SequenceSteps.Contains(SeedStep) Then
                        endReached = True
                        Exit While
                    End If

                    SequenceSteps.Insert(0, refStep)
                    StartInChIKey = refStep.ReactantInChIKey

                Case > 1 'multiple upstream connects -> branch off

                    HasUpstreamConnections = True
                    VerticalConnectorLeft.Visibility = Visibility.Visible

                    'create (recursive) upstream elements 
                    For Each connStep In prevConnects
                        Dim upSequence As New SequenceControl(connStep, SequenceDirection.Upstream) 'recursive
                        upSequence.ShowDownstreamConnector()
                        upSequence.HasDownstreamConnections = True
                        UpstreamSequences.Add(upSequence)   'add to list, not UI
                    Next

                    'detect converging upstream sequences
                    Dim res = From seq In UpstreamSequences Group By seq.StartInChIKey Into convergentGroups = Group

                    For Each result In res

                        Dim upStrElement As New AdjacentSequenceItem

                        If result.convergentGroups.Count = 1 Then

                            'no convergence -> add next connecting sequence to downstream panel
                            Dim seq = result.convergentGroups.First
                            upStrElement.pnlConvSequences.Children.Add(seq)
                            UpstreamSequenceNr -= 1
                            seq.SequenceTitle = "Sequence " + UpstreamSequenceNr.ToString

                        Else

                            'convergent sequences -> add converging sequence group

                            UpstreamSequenceNr -= 1

                            Dim pos As Integer = 0
                            For Each seq In result.convergentGroups
                                seq.ShowUpstreamConnector()
                                seq.HasUpstreamConnections = True
                                seq.SequenceTitle = "Sequence " + UpstreamSequenceNr.ToString + NumberToCharacter(pos)
                                pos += 1
                                upStrElement.pnlConvSequences.Children.Add(seq)
                            Next

                            Dim firstStep = result.convergentGroups.First.SequenceSteps.First
                            If firstStep.GetPreviousSteps.Count > 0 Then

                                'merging to a upstream sequence

                                Dim connStep = firstStep.GetPreviousSteps.First
                                Dim convergedSequence As New SequenceControl(connStep, SequenceDirection.Upstream) 'recursive
                                With convergedSequence
                                    .ShowDownstreamConnector()
                                    .HasDownstreamConnections = True
                                    UpstreamSequenceNr -= 1
                                    .SequenceTitle = "Sequence " + UpstreamSequenceNr.ToString
                                End With

                                With upStrElement
                                    .VerticalConnectorLeft.Visibility = Visibility.Visible
                                    .pnlConvergingUp.Visibility = Visibility.Visible
                                    .pnlConvergingUp.Children.Add(convergedSequence)
                                End With

                            Else

                                'merging to common product without further sequence

                                With upStrElement
                                    .btnMergeRightCenter.Visibility = Visibility.Visible
                                    .pnlConvergingDown.Visibility = Visibility.Visible
                                End With

                            End If
                        End If

                        pnlUpstream.Children.Add(upStrElement)

                    Next

                    endReached = True

                    ShowUpstreamConnector()

            End Select

        End While

    End Sub


    Private Sub ShowUpstreamConnector()

        upConnector.Visibility = Visibility.Visible

    End Sub


    Private Sub ShowDownstreamConnector()

        downConnector.Visibility = Visibility.Visible

    End Sub


    Private Sub btnMain_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnMain.Click

        btnMain.IsChecked = True
        RaiseEvent SequenceSelected(Me)

    End Sub


    ''' <summary>
    ''' Typically used for unchecking previous button when another one is checked
    ''' </summary>
    '''
    Public Sub dlgSequences_ClearSequenceSelections(sender As Object)

        btnMain.IsChecked = False

    End Sub


    ''' <summary>
    ''' Highlights and selects this SequenceControl
    ''' </summary>
    ''' 
    Public Sub HighlightControl()

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

    Public Property IsSelected As Boolean = False

    ''' <summary>
    ''' Sets or gets if the current sequence scheme is based on this step.
    ''' </summary>
    ''' 
    Public Property IsSeedStep As Boolean = False


    ''' <summary>
    ''' Sets or gets the SequenceStructure of this step, which was assigned during scheme population.
    ''' </summary>
    ''' 
    Public Property AssignedSequenceStructure As SequenceStructure


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









