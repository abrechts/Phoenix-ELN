Imports System.Windows.Controls
Imports ElnBase
Imports ElnCoreModel


Public Class SequenceStep

    Public Sub New(reactInChIKey As String, prodInChIKey As String, stepExpList As IEnumerable(Of tblExperiments), dbContext As ElnDbContext)

        ReactantInChIKey = reactInChIKey
        ProductInChIKey = prodInChIKey
        DatabaseContext = dbContext
        StepExperiments = stepExpList

    End Sub

    Public Property DatabaseContext As ElnDbContext

    Public Property ReactantInChIKey As String

    Public Property ProductInChIKey As String

    Public Property IsSelected As Boolean = False

    Public Property IsReactantRacemate As Integer = 0

    Public Property IsProductRacemate As Integer = 0


    ''' <summary>
    ''' Gets all experiments associated with this step
    ''' </summary>
    ''' 
    Public Property StepExperiments As New List(Of tblExperiments)


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
    ''' Gets the list of all SequenceSteps present in the overall sequences scheme.
    ''' </summary>
    '''
    Public Shared Property AllSchemeSteps As New List(Of SequenceStep)


    ''' <summary>
    ''' Gets the next neighboring downstream connecting step(s) based on the current product InChIKey and associated racemate definition. 
    ''' If an empty list is returned, then the end of the sequence is reached. If more than one connecting step is returned, 
    ''' then the sequence is to be completed due to sequence branching.
    ''' </summary>
    ''' 
    Public Function GetNextSteps() As List(Of SequenceStep)

        Dim res As New List(Of SequenceStep)

        Dim nextStepExperiments = DatabaseContext.tblExperiments.Where(Function(exp) _
            exp.ReactantInChIKey = ProductInChIKey _    'match the current product InChIKey to the next step reactant InChIKey
            AndAlso exp.IsRacemicReactant = IsProductRacemate _  'compare the user-specified racemate definition
            AndAlso (exp.ReactantInChIKey <> exp.ProductInChIKey OrElse exp.IsRacemicReactant <> exp.IsRacemicProduct)) 'exclude steps with identical reactant and product 

        ' Group the next step experiments by their user-specified racemate definitions
        Dim expGroupDict = nextStepExperiments.GroupBy(Function(e) e.IsRacemicProduct).ToDictionary(Function(g) g.Key, Function(g) g.ToList())

        For Each kvp In expGroupDict

            Dim isRac = kvp.Key
            For Each prodKey In kvp.Value.Select(Function(e) e.ProductInChIKey).Distinct()
                Dim expList = kvp.Value.Where(Function(e) e.ProductInChIKey = prodKey).ToList
                Dim newStep As New SequenceStep(ProductInChIKey, prodKey, expList, DatabaseContext) With {
                .IsReactantRacemate = IsProductRacemate,
                .IsProductRacemate = isRac
                }
                res.Add(newStep)
            Next

        Next

        Return res

    End Function


    ''' <summary>
    ''' Gets the previous upstream connecting step(s) based on the current reactant InChIKey nChIKey and associated racemate definition. 
    ''' If an empty list is returned, then the end of the sequence is reached. If more than one connecting step is returned, 
    ''' then the sequence is to be completed due to sequence branching.
    ''' </summary>
    ''' 
    Public Function GetPreviousSteps() As List(Of SequenceStep)

        Dim res As New List(Of SequenceStep)

        Dim prevStepExperiments = DatabaseContext.tblExperiments.Where(Function(exp) _
            exp.ProductInChIKey = ReactantInChIKey _    'match the current product InChIKey to the next step reactant InChIKey
            AndAlso exp.IsRacemicProduct = IsReactantRacemate _  'compare the user-specified racemate definition
            AndAlso (exp.ReactantInChIKey <> exp.ProductInChIKey OrElse exp.IsRacemicReactant <> exp.IsRacemicProduct)) 'exclude steps with identical reactant and product 

        ' Group the previous step experiments by their user-specified racemate definitions
        Dim expGroupDict = prevStepExperiments.GroupBy(Function(e) e.IsRacemicReactant).ToDictionary(Function(g) g.Key, Function(g) g.ToList())

        For Each kvp In expGroupDict

            Dim isRac = kvp.Key
            For Each reactKey In kvp.Value.Select(Function(e) e.ReactantInChIKey).Distinct()
                Dim expList = kvp.Value.Where(Function(e) e.ReactantInChIKey = reactKey).ToList
                Dim newStep As New SequenceStep(reactKey, ReactantInChIKey, expList, DatabaseContext) With {
                    .IsReactantRacemate = isRac,
                    .IsProductRacemate = IsReactantRacemate
                }
                res.Add(newStep)
            Next

        Next

        Return res

    End Function


    ''' <summary>
    ''' Gets the reference reactant image as canvas
    ''' </summary>
    ''' 
    Public Function GetReactantImage() As Canvas

        Dim firstExpRxnSketch = StepExperiments.First.RxnSketch
        Dim skInfo = DrawingEditor.GetSketchInfo(firstExpRxnSketch)
        Return skInfo.Reactants.First.StructureCanvas

    End Function


    ''' <summary>
    ''' Gets the reference product image as canvas
    ''' </summary>
    ''' 
    Public Function GetProductImage() As Canvas

        Dim firstExpRxnSketch = StepExperiments.First.RxnSketch
        Dim skInfo = DrawingEditor.GetSketchInfo(firstExpRxnSketch)
        Return skInfo.Products.First.StructureCanvas

    End Function


End Class
