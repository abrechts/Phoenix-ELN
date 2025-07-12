Imports System.Windows.Controls
Imports ElnBase
Imports ElnCoreModel

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
    ''' Gets if the step contains finalized experiments (important for processing server experiments)
    ''' </summary>
    ''' 
    Public ReadOnly Property ContainsFinalizedExperiments As Boolean
        Get

            Dim res = (From exp In DatabaseContext.tblExperiments Where exp.ReactantInChIKey = ReactantInChIKey AndAlso exp.ProductInChIKey = ProductInChIKey _
             AndAlso exp.WorkflowState = ELNEnumerations.WorkflowStatus.Finalized).FirstOrDefault

            Return res IsNot Nothing

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
