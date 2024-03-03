
Imports com.epam.indigo

Public Class RxnSubstructure


    Private Property IndigoBase As Indigo


    Public Sub New()

        IndigoBase = New Indigo

    End Sub

    Public Function MatchReaction(sourceRxn As String, queryRxn As String) As Boolean

        'load source reaction and remove all reactants except first one (i.e. ref. reactant)
        Dim srcReactionOrig = IndigoBase.loadReaction(sourceRxn)
        Dim srcReaction = RemoveExcessReactants(srcReactionOrig)
        srcReaction.aromatize()
        srcReaction.automap()

        'load query reaction
        Dim queryReaction = IndigoBase.loadQueryReaction(queryRxn)
        queryReaction.aromatize()
        queryReaction.automap()

        '   Dim srcRxnSerialized = srcReaction.serialize

        '  For i = 1 To 10000
        '  srcReaction = indigo.loadReaction(srcRxnSerialized)
        'indigo.substructureMatcher(srcReaction).match(queryReaction)
        '  Next

        'perform query

        Dim match = IndigoBase.substructureMatcher(srcReaction).match(queryReaction)

        Return match IsNot Nothing

    End Function


    Private Function RemoveExcessReactants(origReaction As IndigoObject) As IndigoObject

        Dim srcReaction = IndigoBase.createReaction

        For Each react As IndigoObject In origReaction.iterateReactants
            srcReaction.addReactant(react)
            Exit For
        Next
        For Each prod As IndigoObject In origReaction.iterateProducts
            srcReaction.addProduct(prod)
        Next

        Return srcReaction

    End Function


End Class


