Imports System.Windows.Documents
Imports com.epam.indigo
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

' * Performance notes: 
' * MatchRxnFingerprint: 36 ms for 100'000 comparisons (comparison code only, no LINQ database access overhead)
' * MatchRxnSubstructure: 240 ms for 1000 comparisons (using serialized rxnObject overload, comparison code only, no LINQ database access overhead)
' * --> Fingerprint matching is performed first, then substructure query on newSmarts for hit confirmation.

Public Enum RssErrorType

    None
    TooManyHits
    QueryStructureError

End Enum


Public Class RxnSubstructure

    Private Property IndigoBase As Indigo


    Public Sub New()

        IndigoBase = New Indigo

    End Sub


    ''' <summary>
    ''' Sets or gets the maximum number of fingerprint hits before too many hits are reported.
    ''' </summary>
    ''' 
    Public Shared Property FingerPrintCutOffLimit As Integer = 500


    ''' <summary>
    ''' Gets all *finalized* experiment entries with a reaction sketch containing the specified substructure reaction.
    ''' </summary>
    ''' <param name="queryRxnStr">MDL reaction file string of the query substructure reaction to subMatch.</param>
    ''' <param name="dbContext">Database context for query.</param>
    ''' <param Name="fpOnly">Optional: The reaction fingerprints are not confirmed by subsequent reaction substructure tests. 
    ''' This is an extremely fast search, but most likely will contain some false positives. E.g. chiral query centers are 
    ''' ignored in this mode.</param>
    ''' <returns>Query hits as IEnumerable of tblExperiments, or empty IEnumerable if not hits found.</returns>
    ''' 
    Public Function PerformRssQuery(queryRxnStr As String, dbContext As ElnDataContext, isFromServer As Boolean) As RssQueryResult

        Dim rssResult As New RssQueryResult

        Dim queryRxnObj = GetMappedIndigoRxn(queryRxnStr, True)

        If queryRxnObj Is Nothing Then
            'most likely a structure error (e.g. impossible chirality)
            With rssResult
                .ExperimentHits = Nothing
                .ErrorType = RssErrorType.QueryStructureError
            End With
            Return rssResult
        End If

        Dim queryFp = queryRxnObj.fingerprint("sub")

        Dim fpRes As IEnumerable(Of tblExperiments)
        If Not isFromServer Then
            fpRes = From exp In dbContext.tblExperiments.AsEnumerable Where MatchRxnFingerpint(exp.RxnFingerprint, queryFp)
        Else
            fpRes = From exp In dbContext.tblExperiments.AsEnumerable Where exp.WorkflowState = WorkflowStatus.Finalized AndAlso
              MatchRxnFingerpint(exp.RxnFingerprint, queryFp)
        End If

        If fpRes.Any Then

            If fpRes.Count <= FingerPrintCutOffLimit Then

                'confirm fingerprint hits by substructure match
                Dim rssRes = From exp In fpRes Where MatchRxnSubstructure(exp.RxnIndigoObj, queryRxnObj)
                With rssResult
                    .ExperimentHits = rssRes
                    .ErrorType = RssErrorType.None
                End With
                Return rssResult

            Else

                'maximum fingerprint hit limit reached
                With rssResult
                    .ExperimentHits = Nothing
                    .ErrorType = RssErrorType.TooManyHits
                End With
                Return rssResult

            End If

        Else

            'no fingerprint hits found
            With rssResult
                .ExperimentHits = fpRes 'empty IEnumerable
                .ErrorType = RssErrorType.None  'no hits is not an error ....
            End With
            Return rssResult

        End If

    End Function


    ''' <summary>
    ''' Perform a backlog RSS registration for the specified user entry (only required for app version < 0.9.4)
    ''' </summary>
    ''' 
    Public Sub RegisterRssBacklog(userEntry As tblUsers)

        For Each exp In userEntry.tblExperiments
            Dim ok = RegisterReactionRSS(exp)    'ignore if registration failed  (ok=false)
        Next

    End Sub


    ''' <summary>
    ''' Registers the reaction sketch properties of the specified experiment which are required for performing RSS searches.
    ''' </summary>
    ''' <returns>True if the registration was successful, false if the reaction sketch was invalid. </returns>
    ''' 
    Public Function RegisterReactionRSS(ByRef expEntry As tblExperiments) As Boolean

        If expEntry.MDLRxnFileString <> "" Then

            Dim rxnObj = GetMappedIndigoRxn(expEntry.MDLRxnFileString, False)

            If rxnObj IsNot Nothing Then

                expEntry.RxnIndigoObj = rxnObj.serialize
                expEntry.RxnFingerprint = rxnObj.fingerprint("full").toBuffer
                Return True

            Else

                Return False

            End If

        Else

            Return False

        End If

    End Function


    ''' <summary>
    ''' Gets if specified source reaction fingerprint matches the specified query reaction fingerprint.
    ''' </summary>
    ''' 
    Private Function MatchRxnFingerpint(fpSourceArr As Byte(), fpQuery As IndigoObject) As Boolean

        If fpSourceArr IsNot Nothing Then

            Dim fpSource = IndigoBase.loadFingerprintFromBuffer(fpSourceArr)
            Dim commonRxnBits = IndigoBase.commonBits(fpSource, fpQuery)

            Return (commonRxnBits = fpQuery.countBits)

        Else

            Return False

        End If

    End Function


    ''' <summary>
    ''' Gets if the specified serialized reaction object and the specified reaction substructure newSmarts in a RSS substructure hit.
    ''' </summary>
    ''' 
    Private Function MatchRxnSubstructure(srcIndigoRxnArr As Byte(), queryIndigoRxnObj As IndigoObject) As Boolean

        Dim srcIndigoObj = IndigoBase.loadReaction(srcIndigoRxnArr)

        Return MatchRxnSubstructure(srcIndigoObj, queryIndigoRxnObj)

    End Function


    ''' <summary>
    ''' Gets if the specified reaction object and the specified reaction substructure result in a RSS hit.
    ''' </summary>
    ''' 
    Private Function MatchRxnSubstructure(sourceIndigoRxnObj As IndigoObject, queryIndigoRxnObj As IndigoObject) As Boolean

        '240 ms for 1000 hits

        If sourceIndigoRxnObj Is Nothing OrElse queryIndigoRxnObj Is Nothing Then
            Return Nothing
        End If

        'get query reactant and product fragments
        Dim reactFrag As IndigoObject = queryIndigoRxnObj.iterateReactants(0)
        Dim prodFrag As IndigoObject = queryIndigoRxnObj.iterateProducts(0)

        'get source reactant and products
        Dim srcRefReact = sourceIndigoRxnObj.iterateReactants(0)
        Dim srcRefProd = sourceIndigoRxnObj.iterateProducts(0)

        'get unique match counts
        Dim rrCount = UniqueMatchCount(srcRefReact, reactFrag) 'reactFrags in reactant
        Dim prCount = UniqueMatchCount(srcRefReact, prodFrag)  'prodFrags in reactant
        Dim rpCount = UniqueMatchCount(srcRefProd, reactFrag) 'reactFrags in product
        Dim ppCount = UniqueMatchCount(srcRefProd, prodFrag)  'prodFrags in product

        'get query molecules as non-query molecules for subsequent inter-match
        Dim stdReactFrag = IndigoBase.loadMolecule(reactFrag.smiles)
        Dim stdProdFrag = IndigoBase.loadMolecule(prodFrag.smiles)

        'correct for query reactant fragment being part of product fragment
        Dim reactInProdCount = UniqueMatchCount(stdProdFrag, reactFrag)
        rpCount -= reactInProdCount

        'correct for query product fragment being part of reactant fragment
        Dim prodInReactCount = UniqueMatchCount(stdReactFrag, prodFrag)
        prCount -= prodInReactCount

        '--> match, if reactFrag count decreases and prodFrag count increases in srcRefReact -> srcRefProd
        Return (rrCount > rpCount) AndAlso (prCount < ppCount)

    End Function


    ''' <summary>
    ''' Gets the number of *unique* matches between the specified source molecule and the specified query fragment.
    ''' </summary>
    ''' <remarks>Indigo usually returns a number of geometrically possible matches for one single match. 
    ''' UniqueMatchCount reduces this to one match per same same match geometry.</remarks>
    ''' 
    Private Function UniqueMatchCount(srcComponent As IndigoObject, queryFragment As IndigoObject) As Integer

        Dim substrMatcher As IndigoObject = IndigoBase.substructureMatcher(srcComponent)
        Dim uniqueCount As Integer = 0

        For Each subMatch As IndigoObject In substrMatcher.iterateMatches(queryFragment)

            Dim found As Boolean = False

            For Each queryAtom As IndigoObject In queryFragment.iterateAtoms
                Dim matchedAtom As IndigoObject = subMatch.mapAtom(queryAtom)   'gets source atom (non-query)
                If matchedAtom IsNot Nothing Then
                    If Not matchedAtom.isHighlighted Then
                        matchedAtom.highlight()
                        found = True
                    Else
                        found = False
                        Exit For
                    End If
                End If
            Next

            If found Then
                uniqueCount += 1
            End If

        Next

        srcComponent.unhighlight()
        Return uniqueCount

    End Function


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
                    'query reaction
                    indigoRxn = IndigoBase.loadQueryReaction(mdlRxnString) 'GetQueryReaction(mdlRxnString)
                Else
                    'source reaction
                    indigoRxn = IndigoBase.loadReaction(mdlRxnString)
                End If

                'remove non-reference reactants if specified (default)
                If refReactantOnly Then
                    indigoRxn = RemoveExcessReactants(indigoRxn, isQueryRxn)
                End If

                indigoRxn.aromatize()

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

        If srcReaction.countReactants = 1 Then
            Return srcReaction
        End If

        Dim dstReaction As IndigoObject

        If Not isQueryRxn Then
            dstReaction = IndigoBase.createReaction
        Else
            dstReaction = IndigoBase.createQueryReaction
        End If

        dstReaction.addReactant(srcReaction.iterateReactants(0))
        For Each prod As IndigoObject In srcReaction.iterateProducts
            dstReaction.addProduct(prod)
        Next

        Return dstReaction

    End Function

End Class


Public Class RssQueryResult

    Public Property ExperimentHits As IEnumerable(Of tblExperiments)

    Public Property ErrorType As RssErrorType

End Class





