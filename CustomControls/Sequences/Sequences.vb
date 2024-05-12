

Imports System.Windows.Controls
Imports ElnBase
Imports ElnCoreModel


Public Class ReactionSequence

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
    Public Property DownstreamSequences As List(Of ReactionSequence)


    ''' <summary>
    ''' Sets or gets the list of all directly connected upstream sequences.
    ''' </summary>
    ''' <returns></returns>
    Public Property UpstreamSequences As List(Of ReactionSequence)


    ''' <summary>
    ''' Creates a new reaction sequence based on the specified experiment entry, then recursively adds all downstream and 
    ''' upstream steps. Additional sequences are recursively added on the way.
    ''' </summary>
    ''' <param name="initialExp">The seed experiment to build the sequence around.</param>
    ''' <param name="dbContext">The database context (local or server based).</param>
    ''' 
    Public Sub New(initialExp As tblExperiments, dbContext As ElnDbContext)

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

        StartInChIKey = connectingStep.ReactantInChIKey
        EndInChIKey = connectingStep.ProductInChIKey
        SequenceTitle = "Sequence"

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

        Dim downStreamConnectSequences As New List(Of ReactionSequence)
        Dim endReached As Boolean = False

        While Not endReached
            Dim nextConnects = refStep.GetNextStep
            If nextConnects.Count = 1 Then
                'sequence continues
                refStep = nextConnects.First
                SequenceSteps.Add(refStep)
                EndInChIKey = refStep.ProductInChIKey
            Else
                'end of sequence
                If nextConnects.Count > 1 Then

                    'branch off downstream
                    For Each connStep In nextConnects
                        Dim downSequence As New ReactionSequence(connStep, SequenceDirection.Downstream)
                        downStreamConnectSequences.Add(downSequence)
                    Next

                    'arrange connecting sequences into groups of sequences with identical product (converging sequences)
                    Dim res = From seq In downStreamConnectSequences Group By seq.EndInChIKey Into convergentGroups = Group
                    For Each result In res
                        If result.convergentGroups.Count = 1 Then
                            'no convergence -> add next connecting sequence to downStream panel
                        Else
                            'convergent sequences -> add next sequence to convergent panel of this control
                        End If
                    Next

                End If

                'TODO: detect groups of converging downstream sequences (each group having identical end InChIKey)

                endReached = True 'end of sequence: no more steps, or branching off
            End If
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
                        Dim upSequence As New ReactionSequence(connStep, SequenceDirection.Upstream)
                        UpstreamSequences.Add(upSequence)
                    Next
                End If

                'TODO: detect groups of converging upstream sequences (each group having identical start InChIKey)

                endReached = True 'start of sequence: no more steps, or branching off
            End If
        End While

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
