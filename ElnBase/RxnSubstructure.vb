
Imports com.epam.indigo

Public Class RxnSubstructure

    Private Property IndigoBase As Indigo


    Public Sub New()
        IndigoBase = New Indigo
    End Sub


    ''' <summary>
    ''' Gets if the pre-formed source and query Indigo reaction objects result in a RSS substructure hit.
    ''' </summary>
    ''' 
    Public Function MatchReaction(sourceIndigoRxnObj As IndigoObject, queryIndigoRxnObj As IndigoObject) As Boolean

        If sourceIndigoRxnObj Is Nothing OrElse queryIndigoRxnObj Is Nothing Then
            Return Nothing
        End If

        Dim match = IndigoBase.substructureMatcher(sourceIndigoRxnObj).match(queryIndigoRxnObj)
        Return match IsNot Nothing

    End Function


    ''' <summary>
    ''' Gets if the specified source reaction and query sub reaction, both in MDL rxnFile format, 
    ''' result in a RSS substructure hit.
    ''' </summary>
    ''' <param name="sourceMdlRxn">The reaction to test for a hit, as MDL rxnFile string.</param>
    ''' <param name="queryMdlRxn">The query substructure reaction, as MDL rxnFile string.</param>
    ''' <returns>True, if the queryMdlRxn matches the sourceMdlRxn.</returns>
    ''' 
    Public Function MatchReaction(sourceMdlRxn As String, queryMdlRxn As String) As Boolean

        Dim srcReaction = GetMappedIndigoRxn(sourceMdlRxn, False)
        Dim queryReaction = GetMappedIndigoRxn(queryMdlRxn, True)

        Dim fpSource = srcReaction.fingerprint("sub")
        Dim fpQuery = queryReaction.fingerprint("sub")

        'Result: 36 ms for 100'000 comparisons!
        'For i = 1 To 100000
        '    MatchRssFingerpint(fpSource, fpQuery)
        'Next

        Dim srcStr = fpSource.toBuffer  'length = 534
        Dim srcRxnSerialized = srcReaction.serialize '501 for arnaould

        If MatchRssFingerpint(fpSource, fpQuery) Then
            Return MatchReaction(srcReaction, queryReaction)
        Else
            MsgBox("No fingerprint hit!")
            Return False
        End If


        '  For i = 1 To 10000
        '  dstReaction = indigo.loadReaction(srcRxnSerialized)
        'indigo.substructureMatcher(dstReaction).match(queryReaction)
        '  Next

    End Function


    ''' <summary>
    ''' Gets if specified source reaction fingerprint matches the specified query reaction fingerprint.
    ''' </summary>
    ''' 
    Public Function MatchRssFingerpint(fpSource As IndigoObject, fpQuery As IndigoObject) As Boolean

        Dim commonRxnBits = IndigoBase.commonBits(fpSource, fpQuery)
        Return (commonRxnBits = fpQuery.countBits)

    End Function


    ''' <summary>
    ''' ^Converts the specified MDL rxnFile string into a mapped Indigo reaction object.
    ''' </summary>
    ''' <param name="isQueryRxn">True, if a query reaction is to be returned.</param>
    ''' <param name="refReactantOnly">Optional: True, if all reactants except the reference one is to be removed (default=true).</param>
    ''' <returns>Nothing if an error occurred.</returns>
    '''
    Public Function GetMappedIndigoRxn(mdlRxnString As String, isQueryRxn As Boolean, Optional refReactantOnly As Boolean = True) As IndigoObject

        If mdlRxnString <> "" Then

            Try

                Dim indigoRxn As IndigoObject

                If isQueryRxn Then
                    'load as query reaction
                    indigoRxn = IndigoBase.loadQueryReaction(mdlRxnString)
                Else
                    'load as standard reaction
                    indigoRxn = IndigoBase.loadReaction(mdlRxnString)
                End If

                'remove non-reference reactants if specified (default)
                If refReactantOnly Then
                    indigoRxn = RemoveExcessReactants(indigoRxn, isQueryRxn)
                End If

                'prepare for queries
                indigoRxn.aromatize()
                indigoRxn.automap()

                Return indigoRxn

            Catch ex As Exception

                Return Nothing

            End Try

        Else

            Return Nothing

        End If

    End Function


    ''' <summary>
    ''' Removes all reactants except the first one (the reference) from the specified  
    ''' reaction and returns the resulting reaction.
    ''' </summary>
    ''' 
    Private Function RemoveExcessReactants(srcReaction As IndigoObject, isQueryRxn As Boolean) As IndigoObject

        Dim dstReaction As IndigoObject

        If Not isQueryRxn Then
            dstReaction = IndigoBase.createReaction
        Else
            dstReaction = IndigoBase.createQueryReaction
        End If

        For Each react As IndigoObject In srcReaction.iterateReactants
            dstReaction.addReactant(react)
            Exit For
        Next
        For Each prod As IndigoObject In srcReaction.iterateProducts
            dstReaction.addProduct(prod)
        Next

        Return dstReaction

    End Function


End Class


