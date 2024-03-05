
Imports com.epam.indigo
Imports ElnCoreModel

Public Class RxnSubstructure

    Private Property IndigoBase As Indigo


    Public Sub New()
        IndigoBase = New Indigo
    End Sub


    ''' <summary>
    ''' Gets all experiment entries with a reaction sketch containing the specified substructure reaction.
    ''' </summary>
    ''' <param name="queryRxnStr">MDL reaction file string of the query substructure reaction to match.</param>
    ''' <param name="dbContext">Database context for query.</param>
    ''' <param Name="fpOnly">Optional: The reaction fingerprints are not confirmed by subsequent reaction substructure tests. 
    ''' This is an extremely fast search, but most likely will contain some false positives. E.g. chiral query centers are 
    ''' ignored in this mode.</param>
    ''' <returns>Query hits as IEnumerable of tblExperiments, or empty IEnumerable if not hits found.</returns>
    ''' 
    Public Function PerformRssQuery(queryRxnStr As String, dbContext As ElnDataContext, Optional fpOnly As Boolean = False) As IEnumerable(Of tblExperiments)

        Dim queryRxnObj = GetMappedIndigoRxn(queryRxnStr, True)
        Dim queryFp = queryRxnObj.fingerprint("sub")

        Dim fpRes = From exp In dbContext.tblExperiments.AsEnumerable Where MatchRssFingerpint(exp.RxnFingerprint, queryFp)

        If fpOnly Then
            Return fpRes
        End If

        If fpRes.Any Then

            'confirm fingerprint hits by substructure macht
            Dim rssRes = From exp In fpRes Where MatchReaction(exp.RxnIndigoObj, queryRxnObj)
            If rssRes.Any Then
                Debug.WriteLine("Hits: " + fpRes.Count.ToString + "-fp; " + rssRes.Count.ToString + "-rss")
            Else
                Debug.WriteLine("Hits: " + fpRes.Count.ToString + "-fp; 0-rss")
            End If

            Return rssRes

        Else

            Debug.WriteLine("No Hit!")
            Return fpRes

        End If

    End Function


    ''' <summary>
    ''' Perform a backlog RSS registration for the specified user entry (only required for app version < 0.9.4)
    ''' </summary>
    ''' 
    Public Sub RegisterRssBacklog(userEntry As tblUsers)

        For Each exp In userEntry.tblExperiments
            RegisterReactionRss(exp)
        Next

    End Sub


    ''' <summary>
    ''' Register the reaction properties of the specified experiment entry required for performing RSS searches.
    ''' </summary>
    ''' 
    Public Sub RegisterReactionRss(ByRef expEntry As tblExperiments)

        If expEntry.MDLRxnFileString <> "" Then

            Dim rxnObj = GetMappedIndigoRxn(expEntry.MDLRxnFileString, False)
            expEntry.RxnIndigoObj = rxnObj.serialize
            expEntry.RxnFingerprint = rxnObj.fingerprint("sub").toBuffer

        End If

    End Sub



    ''' <summary>
    ''' Gets if specified source reaction fingerprint matches the specified query reaction fingerprint.
    ''' </summary>
    ''' 
    Private Function MatchRssFingerpint(fpSourceArr As Byte(), fpQuery As IndigoObject) As Boolean

        If fpSourceArr IsNot Nothing Then

            Dim fpSource = IndigoBase.loadFingerprintFromBuffer(fpSourceArr)
            Dim commonRxnBits = IndigoBase.commonBits(fpSource, fpQuery)

            Return (commonRxnBits = fpQuery.countBits)

        Else

            Return False

        End If

    End Function


    Private Function MatchReaction(srcIndigoRxnArr As Byte(), queryIndigoRxnObj As IndigoObject) As Boolean

        Dim srcIndigoObj = IndigoBase.loadReaction(srcIndigoRxnArr)
        Return MatchReaction(srcIndigoObj, queryIndigoRxnObj)

    End Function


    ''' <summary>
    ''' Gets if the pre-formed source and query Indigo reaction objects result in a RSS substructure hit.
    ''' </summary>
    ''' 
    Private Function MatchReaction(sourceIndigoRxnObj As IndigoObject, queryIndigoRxnObj As IndigoObject) As Boolean

        If sourceIndigoRxnObj Is Nothing OrElse queryIndigoRxnObj Is Nothing Then
            Return Nothing
        End If

        Dim match = IndigoBase.substructureMatcher(sourceIndigoRxnObj).match(queryIndigoRxnObj)
        Return match IsNot Nothing

    End Function


    '''' <summary>
    '''' Gets if the specified source reaction and query sub reaction, both in MDL rxnFile format, 
    '''' result in a RSS substructure hit.
    '''' </summary>
    '''' <param name="sourceMdlRxn">The reaction to test for a hit, as MDL rxnFile string.</param>
    '''' <param name="queryMdlRxn">The query substructure reaction, as MDL rxnFile string.</param>
    '''' <returns>True, if the queryMdlRxn matches the sourceMdlRxn.</returns>
    '''' 
    'Friend Function MatchReaction(sourceMdlRxn As String, queryMdlRxn As String) As Boolean

    '    Dim srcReaction = GetMappedIndigoRxn(sourceMdlRxn, False)
    '    Dim queryReaction = GetMappedIndigoRxn(queryMdlRxn, True)

    '    Dim fpSource = srcReaction.fingerprint("sub")
    '    Dim fpQuery = queryReaction.fingerprint("sub")

    '    'Result: 36 ms for 100'000 comparisons!
    '    'For i = 1 To 100000
    '    '    MatchRssFingerpint(fpSource, fpQuery)
    '    'Next

    '    Dim srcStr = fpSource.toBuffer  'length = 534
    '    Dim srcRxnSerialized = srcReaction.serialize '501 for mid-size structure

    '    If MatchRssFingerpint(fpSource.toBuffer, fpQuery) Then
    '        Return MatchReaction(srcReaction, queryReaction)
    '    Else
    '        MsgBox("No fingerprint hit!")
    '        Return False
    '    End If

    '    '  For i = 1 To 10000
    '    '  dstReaction = indigo.loadReaction(srcRxnSerialized)
    '    'indigo.substructureMatcher(dstReaction).match(queryReaction)
    '    '  Next

    'End Function


    ''' <summary>
    ''' ^Converts the specified MDL rxnFile string into a mapped Indigo reaction object.
    ''' </summary>
    ''' <param name="isQueryRxn">True, if a query reaction is to be returned.</param>
    ''' <param name="refReactantOnly">Optional: True, if all reactants except the reference one is to be removed (default=true).</param>
    ''' <returns>Nothing if an error occurred.</returns>
    '''
    Private Function GetMappedIndigoRxn(mdlRxnString As String, isQueryRxn As Boolean, Optional refReactantOnly As Boolean = True) As IndigoObject

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


