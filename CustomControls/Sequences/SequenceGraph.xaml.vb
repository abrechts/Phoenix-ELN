
Imports System.ComponentModel
Imports System.Text
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Shapes

Partial Public Class SequenceGraph
    Inherits UserControl


    ' Layout metrics
    ' ---------------------------------------

    Private Const BOX_W As Double = 130      ' card width
    Private Const BOX_H As Double = 44       ' card height
    Private Const COL_GAP As Double = 70     ' horizontal gap between columns
    Private Const ROW_GAP As Double = 30     ' vertical gap between cards in same column
    Private Const PAD As Double = 9          ' canvas padding on every side
    Private Const EDGE_GAP As Double = 5     ' horizontal gap between a card edge and where its connecting line starts/ends


    ' Domain model
    ' ------------------------------------------

    ''' <summary>
    ''' An object that exposes two connector keys A and B.
    ''' </summary>
    '''
    Public Class GraphObject

        Public Property A As String = ""            ' "from" key
        Public Property B As String = ""            ' "to"   key
        Public Property IsSeed As Boolean = False   ' sets or gets if this is the central seed object of the graph 
        Public Property Title As String = ""        ' optional display name
        Public Property AttachedItem As Object      ' optional native object to carry along

        Public Overrides Function ToString() As String
            Return Title ' $"{A}→{B}"
        End Function

    End Class


    ''' <summary>
    ''' A GraphObject wrapped in a directed graph node.
    ''' </summary>
    '''
    Public Class GraphNode

        Public Property ConnectItem As GraphObject
        Public Property Downstream As New List(Of GraphNode)   ' successors (obj.B → next.A)
        Public Property Upstream As New List(Of GraphNode)   ' predecessors

    End Class


    ''' <summary>
    ''' A maximal linear chain of GraphNodes compressed into a single visual box.
    ''' </summary>
    '''
    Public Class SequenceNode

        Public Property Id As Integer
        Public Property Members As New List(Of GraphNode)
        Public Property Outgoing As New List(Of SequenceNode)
        Public Property Incoming As New List(Of SequenceNode)
        Public Property ContainsSeed As Boolean = False
        Public Property IsParallel As Boolean = False
        Public Property SequenceType As String

        ' Layout fields (populated by ComputeLayout)
        Public Property Level As Integer = -1
        Public Property X As Double
        Public Property Y As Double
        Public Property W As Double = BOX_W
        Public Property H As Double = BOX_H

        ''' <summary>
        ''' True only when this sequence is a genuine central connection point:
        ''' at least two upstream sequences flow IN  (value appears as multiple B's)
        ''' AND at least two downstream sequences flow OUT (value appears as multiple A's).
        ''' A pure fork (many out, 0 in) stays LINEAR; a pure merge (many in, 0 out)
        ''' stays TERMINAL.  Both conditions must hold simultaneously for HUB.
        ''' </summary>
        '''
        Public ReadOnly Property IsHub As Boolean
            Get
                Return Incoming.Count >= 2 AndAlso Outgoing.Count >= 2
            End Get
        End Property


        ''' <summary>
        ''' Gets if the sequence stops at one of its ends.
        ''' </summary>
        ''' 
        Public ReadOnly Property IsTerminal As Boolean
            Get
                Return Outgoing.Count = 0
            End Get
        End Property

    End Class

    ''' <summary>
    ''' A purely visual routing helper: when M≥2 sources all share the same N≥2
    ''' targets a single junction dot replaces the M×N crossing bezier curves
    ''' with M + N non-crossing segments, one per source and one per target.
    ''' </summary>
    '''
    Private Class JunctionBundle

        Public Property Sources As New List(Of SequenceNode)
        Public Property Targets As New List(Of SequenceNode)
        Public Property Jx As Double   ' dot X
        Public Property Jy As Double   ' dot Y

    End Class


    Private _objects As New List(Of GraphObject)
    Private _sequences As New List(Of SequenceNode)
    Private _zoom As Double = 1.0

    ' Selection state: maps each rendered sequence to its card border, so the
    ' card can be (re-)highlighted both on click and from code-behind.
    Private _seqCards As New Dictionary(Of SequenceNode, Border)
    Private _selectedSeq As SequenceNode = Nothing

    Private Shared ReadOnly SelectedBorderBrush As New SolidColorBrush(Colors.Yellow)

    Public Event SequenceCardClicked(seq As SequenceNode)


    '═══════════════════════════════════════════════════════════════════════════
    ' Color helpers  (avoid CByte noise everywhere)
    '═══════════════════════════════════════════════════════════════════════════

    Private Shared Function GetColor(r As Integer, g As Integer, b As Integer) As Color
        Return Color.FromRgb(CByte(r), CByte(g), CByte(b))
    End Function

    Private Shared Function GetColor(a As Integer, r As Integer, g As Integer, b As Integer) As Color
        Return Color.FromArgb(CByte(a), CByte(r), CByte(g), CByte(b))
    End Function

    Private Shared Function GetBrush(r As Integer, g As Integer, b As Integer) As SolidColorBrush
        Return New SolidColorBrush(GetColor(r, g, b))
    End Function

    Private Shared Function GetBrush(a As Integer, r As Integer, g As Integer, b As Integer) As SolidColorBrush
        Return New SolidColorBrush(GetColor(a, r, g, b))
    End Function


    ''' <summary>
    ''' Runs the full BFS → sequence-compression → layout → render pipeline for
    ''' the given object set rooted at the supplied seed, and reveals the canvas
    ''' (hiding the hint text). Throws on malformed input — callers should report
    ''' the exception message to the user.
    ''' </summary>
    ''' 
    Public Sub BuildAndRender(objects As List(Of GraphObject), seed As GraphObject)

        _objects = objects

        Dim seedNode = BfsGraph(seed)
        BuildSequences(seedNode)
        ComputeLayout()
        Render()

    End Sub


    Public ReadOnly Property SequenceCount As Integer
        Get
            Return _sequences.Count
        End Get
    End Property


    Public Property SeedSequence As SequenceNode

    ''' <summary>
    ''' Returns the sequences built by the last BuildAndRender call.
    ''' </summary>
    '''
    Public ReadOnly Property Sequences As IReadOnlyList(Of SequenceNode)
        Get
            Return _sequences
        End Get
    End Property

    ''' <summary>
    ''' The height of the rendered graph content (GraphCanvas), as computed by
    ''' the last ComputeLayout pass. Useful for sizing host containers to fit
    ''' the graph without unnecessary scroll space.
    ''' </summary>
    ''' 
    Public ReadOnly Property GraphContentHeight As Double
        Get
            Return GraphCanvas.Height
        End Get
    End Property


    ' Zoom
    ' ----------------------------------------

    Private Const ZOOM_STEP As Double = 0.15
    Private Const ZOOM_MIN As Double = 0.2
    Private Const ZOOM_MAX As Double = 4.0
    Private Const WHEEL_ZOOM_MIN As Double = 0.25
    Private Const WHEEL_ZOOM_MAX As Double = 1.0
    Private Const BASE_SCALE As Double = 0.85

    ' Set while code updates ZoomCombo's selection, so the resulting
    ' SelectionChanged doesn't re-enter ApplyZoom.
    Private _suppressZoomComboEvent As Boolean = False

    Private _baseScale As Double = BASE_SCALE

    ''' <summary>
    ''' The rendering scale that corresponds to a 100% zoom level. Lets a
    ''' host embed the graph at a smaller (or larger) intrinsic size while
    ''' the zoom combo, Ctrl+MouseWheel range and "Fit All" still treat that
    ''' size as "100%".
    ''' </summary>
    '''
    Public Property BaseScale As Double
        Get
            Return _baseScale
        End Get
        Set(value As Double)
            _baseScale = If(value > 0, value, 1.0)
            If GraphCanvas IsNot Nothing Then
                ApplyZoom(_zoom)
                UpdateFitAllAvailability()
            End If
        End Set
    End Property

    Private Sub ZoomCombo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _suppressZoomComboEvent OrElse GraphCanvas Is Nothing Then Return

        Dim item = TryCast(ZoomCombo.SelectedItem, ComboBoxItem)
        If item Is Nothing Then Return

        Select Case CStr(item.Content)
            Case "100%" : ApplyZoom(1.0)
            Case "75%" : ApplyZoom(0.75)
            Case "50%" : ApplyZoom(0.5)
            Case "Fit All" : ApplyFitAllZoom()
        End Select
    End Sub

    ''' <summary>
    ''' Ctrl+MouseWheel zooms the graph in/out by one zoom step per notch,
    ''' using the same step size as the old +/- buttons, clamped to between
    ''' 25% and 100%. Without Ctrl, the wheel event is left untouched so the
    ''' ScrollViewer scrolls normally.
    ''' </summary>
    '''
    Private Sub GraphScroll_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs)

        If Keyboard.Modifiers <> ModifierKeys.Control Then Return

        e.Handled = True
        Dim delta = If(e.Delta > 0, ZOOM_STEP, -ZOOM_STEP)
        ApplyZoom(Math.Min(Math.Max(_zoom + delta, WHEEL_ZOOM_MIN), WHEEL_ZOOM_MAX))

    End Sub


    Private Sub GraphScroll_SizeChanged(sender As Object, e As SizeChangedEventArgs)

        UpdateFitAllAvailability()

    End Sub


    Private Sub ApplyZoom(level As Double)

        _zoom = level
        Dim actualScale = _zoom * _baseScale
        GraphCanvas.LayoutTransform = New ScaleTransform(actualScale, actualScale)
        SyncZoomCombo()

    End Sub


    ''' <summary>
    ''' Returns the zoom level (relative to BaseScale, where 1.0 = 100%) at
    ''' which the whole canvas would fit inside the current ScrollViewer
    ''' viewport, or Nothing when either the canvas or the viewport doesn't
    ''' yet have a usable size.
    ''' </summary>
    '''
    Private Function ComputeFitAllZoom() As Double?

        If GraphCanvas.Width <= 0 OrElse GraphCanvas.Height <= 0 OrElse
           GraphScroll.ViewportWidth <= 0 OrElse GraphScroll.ViewportHeight <= 0 Then
            Return Nothing
        End If

        Dim scaleX = GraphScroll.ViewportWidth / GraphCanvas.Width
        Dim scaleY = GraphScroll.ViewportHeight / GraphCanvas.Height
        Return Math.Min(scaleX, scaleY) / _baseScale

    End Function


    ''' <summary>
    ''' Scales the graph so the whole canvas fits inside the current
    ''' ScrollViewer viewport.
    ''' </summary>
    '''
    Public Sub ApplyFitAllZoom()

        Dim fitZoom = ComputeFitAllZoom()
        If fitZoom Is Nothing Then
            ApplyZoom(1.0)
            Return
        End If

        ApplyZoom(Math.Min(Math.Max(fitZoom.Value, ZOOM_MIN), 1.0))

    End Sub


    ''' <summary>
    ''' "Fit All" is only useful (and offered) when it would actually shrink
    ''' the graph below 100% — i.e. when the canvas doesn't already fit at
    ''' full size.
    ''' </summary>
    '''
    Private Sub UpdateFitAllAvailability()

        Dim fitZoom = ComputeFitAllZoom()
        FitAllItem.IsEnabled = fitZoom.HasValue AndAlso fitZoom.Value < 1.0

    End Sub


    ''' <summary>
    ''' Updates the live zoom percentage shown on the ZoomCombo and selects
    ''' the item matching the current zoom level (100%, 75% or 50%), or
    ''' clears the selection when the level — e.g. set via Ctrl+MouseWheel or
    ''' "Fit All" — doesn't match any of them.
    ''' </summary>
    '''
    Private Sub SyncZoomCombo()

        ZoomCombo.Tag = $"{CInt(Math.Round(_zoom * 100))}%"
        _suppressZoomComboEvent = True

        Try
            Dim target As String = Nothing
            If Math.Abs(_zoom - 1.0) < 0.001 Then
                target = "100%"
            ElseIf Math.Abs(_zoom - 0.75) < 0.001 Then
                target = "75%"
            ElseIf Math.Abs(_zoom - 0.5) < 0.001 Then
                target = "50%"
            End If

            ZoomCombo.SelectedItem = ZoomCombo.Items.OfType(Of ComboBoxItem)().
                FirstOrDefault(Function(i) CStr(i.Content) = target)
        Finally
            _suppressZoomComboEvent = False
        End Try

    End Sub


    ' --------------------------------------------------------------------------
    ' Phase 1 – BFS graph construction
    ' --------------------------------------------------------------------------
    '  Rule: fromObj → toObj  when  toObj.A = fromObj.B
    '
    '  Two strictly directional passes are used instead of one bidirectional
    '  pass.  A bidirectional pass causes "sibling contamination": when we
    '  reach an upstream ancestor and then fan out along its downstream edges
    '  we pull in branches that share the ancestor but are not on the seed's
    '  upstream path (e.g. Iota appears when seed = Theta, because both are
    '  downstream of Eta, but Iota is not an ancestor or descendant of Theta).
    '
    '  Pass 1 – forward  : seed → downstream only (follows directed edges)
    '  Pass 2 – backward : seed → upstream only   (reverses directed edges)
    '    From each upstream ancestor we continue ONLY further upstream, never
    '    sideways into sibling branches.
    ' --------------------------------------------------------------------------

    Private Function BfsGraph(seedObj As GraphObject) As GraphNode

        ' Wrap every object in a node
        Dim nodeMap As New Dictionary(Of GraphObject, GraphNode)
        For Each obj In _objects
            nodeMap(obj) = New GraphNode With {.ConnectItem = obj}
        Next

        ' Wire ALL directed edges up front (fromObj.B = toObj.A → from → to)
        ' Objects with A = B represent no real connection and are excluded from wiring;
        ' such an object ends up isolated (no Up-/Downstream) and, if it is the seed,
        ' is rendered as its own single-member sequence by BuildSequences.
        Dim wireNodes = nodeMap.Values.Where(Function(n) n.ConnectItem.A <> n.ConnectItem.B).ToList()

        For Each fromNode In wireNodes
            For Each toNode In wireNodes
                If String.Equals(toNode.ConnectItem.A, fromNode.ConnectItem.B, StringComparison.Ordinal) AndAlso
                   Not ReferenceEquals(toNode.ConnectItem, fromNode.ConnectItem) Then
                    If Not fromNode.Downstream.Contains(toNode) Then
                        fromNode.Downstream.Add(toNode)
                        toNode.Upstream.Add(fromNode)
                    End If
                End If
            Next
        Next

        Dim seedNode = nodeMap(seedObj)
        Dim reachable As New HashSet(Of GraphNode)
        reachable.Add(seedNode)

        ' ── Pass 1: forward BFS – follow directed edges downstream ───────────

        Dim downQ As New Queue(Of GraphNode)
        downQ.Enqueue(seedNode)
        While downQ.Count > 0
            Dim cur = downQ.Dequeue()
            For Each dn In cur.Downstream
                If reachable.Add(dn) Then
                    downQ.Enqueue(dn)
                End If
            Next
        End While

        ' ── Pass 2: backward BFS – follow directed edges upstream only ────────
        '  We deliberately do NOT expand downstream from upstream ancestors.
        '  This is what excludes sibling branches like Iota when seed = Theta.

        Dim upQ As New Queue(Of GraphNode)
        upQ.Enqueue(seedNode)
        While upQ.Count > 0
            Dim cur = upQ.Dequeue()
            For Each up In cur.Upstream
                If reachable.Add(up) Then
                    upQ.Enqueue(up)
                End If
            Next
        End While

        ' Prune any edges that now point outside the reachable set
        For Each node In reachable
            node.Downstream.RemoveAll(Function(n) Not reachable.Contains(n))
            node.Upstream.RemoveAll(Function(n) Not reachable.Contains(n))
        Next

        Return seedNode

    End Function


    ' --------------------------------------------------------------------------
    ' Phase 2 – Sequence compression
    ' --------------------------------------------------------------------------
    '  A new sequence starts wherever a node is:
    '    • a true source (no upstream)
    '    • a merge point (≥ 2 upstream)
    '    • any direct child of a branch point (parent has ≥ 2 downstream)
    '  The seed is always treated as a starter so _rootSeq is set correctly.
    '
    '  From each starter the algorithm walks forward as long as the chain
    '  stays strictly linear (exactly 1 downstream, not a starter, not yet
    '  assigned), collecting all members into the same SequenceNode.
    ' --------------------------------------------------------------------------

    Private Sub BuildSequences(seedNode As GraphNode)

        _sequences = New List(Of SequenceNode)
        SeedSequence = Nothing

        ' Collect all nodes via bidirectional BFS from seed
        Dim allNodes As New List(Of GraphNode)
        Dim visited As New HashSet(Of GraphNode)
        Dim q As New Queue(Of GraphNode)
        q.Enqueue(seedNode)
        visited.Add(seedNode)
        While q.Count > 0
            Dim cur = q.Dequeue()
            allNodes.Add(cur)
            For Each nb In cur.Downstream.Concat(cur.Upstream)
                If visited.Add(nb) Then
                    q.Enqueue(nb)
                End If
            Next
        End While

        ' Determine starters — purely topology-driven, seed gets no special treatment.
        ' Adding the seed to isStarter would artificially break any linear chain
        ' that runs through it (e.g. Eta→Theta→Kappa→Mu becomes two sequences
        ' instead of one when Theta is the seed).
        Dim isStarter As New HashSet(Of GraphNode)
        For Each n In allNodes
            If n.Upstream.Count = 0 OrElse n.Upstream.Count >= 2 Then
                isStarter.Add(n)
            End If
            If n.Downstream.Count >= 2 Then
                For Each dn In n.Downstream
                    isStarter.Add(dn)
                Next
            End If
        Next

        ' Build sequences
        Dim nodeToSeq As New Dictionary(Of GraphNode, SequenceNode)
        Dim nextId As Integer = 0

        For Each starter In allNodes.Where(Function(n) isStarter.Contains(n))
            If nodeToSeq.ContainsKey(starter) Then Continue For

            Dim seq As New SequenceNode With {.Id = nextId}
            nextId += 1
            Dim cur = starter

            Do
                seq.Members.Add(cur)
                nodeToSeq(cur) = seq
                ' Keep walking only while strictly linear
                If cur.Downstream.Count = 1 AndAlso
                   Not isStarter.Contains(cur.Downstream(0)) AndAlso
                   Not nodeToSeq.ContainsKey(cur.Downstream(0)) Then
                    cur = cur.Downstream(0)
                Else
                    Exit Do
                End If
            Loop

            _sequences.Add(seq)
        Next

        ' Catch any nodes still unassigned (exotic topology / orphan islands)
        For Each n In allNodes
            If Not nodeToSeq.ContainsKey(n) Then
                Dim seq As New SequenceNode With {.Id = nextId}
                nextId += 1
                seq.Members.Add(n)
                nodeToSeq(n) = seq
                _sequences.Add(seq)
            End If
        Next

        ' Wire sequence-level edges from the tail of each sequence
        For Each seq In _sequences
            For Each dn In seq.Members.Last().Downstream
                Dim target As SequenceNode = Nothing
                If nodeToSeq.TryGetValue(dn, target) Then
                    If Not ReferenceEquals(target, seq) AndAlso Not seq.Outgoing.Contains(target) Then
                        seq.Outgoing.Add(target)
                        target.Incoming.Add(seq)
                    End If
                End If
            Next
        Next

        ' Identify _rootSeq as whichever sequence the seed landed in.
        ' (The seed is never forced to be a starter, so it may sit anywhere
        '  inside a longer linear chain — that is exactly the desired behavior.)
        Dim value As SequenceNode = Nothing
        If nodeToSeq.TryGetValue(seedNode, value) Then
            SeedSequence = value
            SeedSequence.ContainsSeed = True
        ElseIf _sequences.Count > 0 Then
            SeedSequence = _sequences(0)
            SeedSequence.ContainsSeed = True
        End If

        CollapseCycles()
        IsolateRingOutputs()

        ' Mark sequences that run in parallel: two or more sequences share the
        ' same first-member A key and the same last-member B key.
        For Each grp In _sequences.
                Where(Function(s) s.Members.Count > 0).
                GroupBy(Function(s) (s.Members.First().ConnectItem.A,
                                     s.Members.Last().ConnectItem.B)).
                Where(Function(g) g.Count() >= 2)
            For Each s In grp
                s.IsParallel = True
            Next
        Next

    End Sub


    ''' <summary>
    ''' A pair of sequences that point to each other (e.g. A→B→A) forms a closed
    ''' loop with no real direction. Both members are merged into a single
    ''' sequence. External neighbors keep their connection to the merged
    ''' sequence; if the same neighbor would connect on both ends (it is both
    ''' upstream and downstream of the ring), the incoming connection wins and
    ''' the outgoing one is dropped.
    ''' </summary>
    '''
    Private Sub CollapseCycles()

        Dim handled As New HashSet(Of SequenceNode)

        For Each start In _sequences.ToList()
            If handled.Contains(start) Then Continue For

            Dim partner = start.Outgoing.FirstOrDefault(Function(c) c.Outgoing.Contains(start))
            If partner Is Nothing Then Continue For

            Dim ring = {start, partner}
            Dim ringSet As New HashSet(Of SequenceNode)(ring)

            Dim merged As New SequenceNode With {.Id = start.Id}
            Dim externalIn As New List(Of SequenceNode)
            Dim externalOut As New List(Of SequenceNode)
            Dim allNeighbors As New HashSet(Of SequenceNode)

            For Each sq In ring
                merged.Members.AddRange(sq.Members)
                If sq.ContainsSeed Then merged.ContainsSeed = True
                If ReferenceEquals(SeedSequence, sq) Then SeedSequence = merged

                For Each pred In sq.Incoming
                    If Not ringSet.Contains(pred) Then
                        allNeighbors.Add(pred)
                        If Not externalIn.Contains(pred) Then externalIn.Add(pred)
                    End If
                Next
                For Each succ In sq.Outgoing
                    If Not ringSet.Contains(succ) Then
                        allNeighbors.Add(succ)
                        If Not externalOut.Contains(succ) Then externalOut.Add(succ)
                    End If
                Next

                handled.Add(sq)
            Next

            ' incoming wins: drop the outgoing side of any neighbor connected on both ends
            externalOut.RemoveAll(Function(s) externalIn.Contains(s))

            ' strip stale references to the ring members, then reconnect the kept neighbors to 'merged'
            For Each nb In allNeighbors
                nb.Incoming.RemoveAll(Function(s) ringSet.Contains(s))
                nb.Outgoing.RemoveAll(Function(s) ringSet.Contains(s))
            Next
            For Each pred In externalIn
                pred.Outgoing.Add(merged)
                merged.Incoming.Add(pred)
            Next
            For Each succ In externalOut
                succ.Incoming.Add(merged)
                merged.Outgoing.Add(succ)
            Next

            For Each sq In ring
                _sequences.Remove(sq)
            Next
            _sequences.Add(merged)
        Next

    End Sub


    ''' <summary>
    ''' A sequence whose first member's reactant key equals its last member's
    ''' product key represents a closed loop (e.g. A→B→A): both its "ends" are
    ''' the same key. Whatever feeds into that key (Incoming) makes anything
    ''' the ring would feed back out to (Outgoing) redundant, so the ring is
    ''' isolated as a dead-end branch and its downstream targets are reconnected
    ''' further upstream instead.
    ''' </summary>
    '''
    Private Sub IsolateRingOutputs()

        For Each seq In _sequences
            If seq.Members.Count = 0 Then Continue For

            Dim head = seq.Members.First().ConnectItem
            Dim tail = seq.Members.Last().ConnectItem

            If head.A = tail.B AndAlso seq.Incoming.Count > 0 AndAlso seq.Outgoing.Count > 0 Then
                For Each target In seq.Outgoing
                    target.Incoming.Remove(seq)
                Next
                seq.Outgoing.Clear()
            End If
        Next

    End Sub


    ' --------------------------------------------------------------------------
    ' Phase 3 – Hierarchical layout
    ' --------------------------------------------------------------------------
    '
    '  The layout is always rooted at the TRUE SOURCES of the sequence graph
    '  (sequences with no incoming connections), regardless of which object
    '  was chosen as the seed.  This fixes the bug where choosing a terminal
    '  seed like Mu caused all sequences to collapse into one column, because
    '  the old code started the forward-BFS from _rootSeq (Mu's sequence) which
    '  had no outgoing edges, leaving every upstream sequence unlevelled.
    '
    '  Algorithm: iterative longest-path (Bellman-Ford style)
    '    • sources get level 0
    '    • every other sequence gets level = max(all its incoming levels) + 1
    '    • repeat until no level changes (converges in ≤ |sequences| passes)
    '
    '  After level assignment the sequences are arranged in columns (one per
    '  level), stacked vertically and centered against the tallest column.
    ' --------------------------------------------------------------------------

    Private Sub ComputeLayout()

        If _sequences.Count = 0 Then Return

        ' ── Reset levels ─────────────────────────────────────────────────────

        For Each sq In _sequences
            sq.Level = -1
        Next

        ' ── Identify true sources (no incoming sequences) ─────────────────────

        Dim sources As New HashSet(Of SequenceNode)(
            _sequences.Where(Function(s) s.Incoming.Count = 0))

        ' Edge case: every sequence is in a cycle – fall back to rootSeq / first

        If sources.Count = 0 Then
            sources.Add(If(SeedSequence, _sequences(0)))
        End If

        ' ── Iterative longest-path level assignment ───────────────────────────
        '  Each pass updates any sequence whose incoming sequences are all
        '  already levelled.  We repeat until nothing changes.

        Dim anyChange = True
        Dim safety = _sequences.Count + 2   ' avoids infinite loop on cycles
        While anyChange AndAlso safety > 0
            anyChange = False
            safety -= 1
            For Each sq In _sequences
                Dim desired As Integer
                If sources.Contains(sq) Then
                    desired = 0
                ElseIf sq.Incoming.Count > 0 AndAlso
                       sq.Incoming.All(Function(s) s.Level >= 0) Then
                    ' Place after the deepest predecessor (longest-path rule)
                    desired = sq.Incoming.Max(Function(s) s.Level) + 1
                Else
                    Continue For   ' predecessor(s) not yet levelled
                End If

                If desired > sq.Level Then
                    sq.Level = desired
                    anyChange = True
                End If
            Next
        End While

        ' ── Fallback: assign any still-unlevelled sequences ───────────────────
        '  This handles islands with only cycle edges or other exotic topologies.

        Dim fallback = If(_sequences.Any(Function(s) s.Level >= 0),
                          _sequences.Max(Function(s) s.Level) + 1,
                          0)
        For Each sq In _sequences
            If sq.Level < 0 Then sq.Level = fallback
        Next

        ' ── Nudge isolated sources to sit adjacent to their targets ───────────
        '  A source that skips directly to a high-level target (e.g. a short
        '  branch feeding straight into the seed) gets level 0 by default, which
        '  places it at the far left even though it belongs near the right end.
        '  Move each such source to (min outgoing level − 1) so the arrow is short.

        For Each sq In _sequences.Where(Function(s) s.Incoming.Count = 0 AndAlso s.Outgoing.Count > 0)
            Dim minTarget = sq.Outgoing.Min(Function(t) t.Level)
            If minTarget - 1 > sq.Level Then
                sq.Level = minTarget - 1
            End If
        Next

        Dim maxLvl = _sequences.Max(Function(s) s.Level)

        ' ── Group by level column ─────────────────────────────────────────────
        Dim byLevel As New Dictionary(Of Integer, List(Of SequenceNode))
        For lvl As Integer = 0 To maxLvl
            Dim pos = lvl   'avoids using iteration variable in lambda expression
            byLevel(pos) = _sequences.Where(Function(s) s.Level = pos).ToList()
        Next

        ' ── Measure tallest column (for vertical centering) ────────────────────
        Dim maxColH = 0.0
        For Each kvp In byLevel
            Dim h = kvp.Value.Count * BOX_H + (kvp.Value.Count - 1) * ROW_GAP
            If h > maxColH Then maxColH = h
        Next

        ' ── Assign X / Y to every sequence ───────────────────────────────────
        For Each kvp In byLevel
            Dim col = kvp.Key
            Dim seqs = kvp.Value
            Dim colH = seqs.Count * BOX_H + (seqs.Count - 1) * ROW_GAP
            Dim y0 = PAD + (maxColH - colH) / 2.0
            For idx As Integer = 0 To seqs.Count - 1
                seqs(idx).X = PAD + col * (BOX_W + COL_GAP)
                seqs(idx).Y = y0 + idx * (BOX_H + ROW_GAP)
                seqs(idx).W = BOX_W
                seqs(idx).H = BOX_H
            Next
        Next

        ' ── Size the canvas ───────────────────────────────────────────────────
        GraphCanvas.Width = PAD * 2 + maxLvl * (BOX_W + COL_GAP) + BOX_W
        GraphCanvas.Height = PAD * 2 + maxColH

    End Sub


    ' --------------------------------------------------------------------------
    ' Phase 4 – Render
    ' --------------------------------------------------------------------------

    Private Sub Render()

        GraphCanvas.Children.Clear()
        _seqCards.Clear()
        _selectedSeq = Nothing

        ' Detect bipartite edge bundles that benefit from a junction dot
        ' (≥2 sources that all share the exact same set of ≥2 targets).

        Dim bundles = FindJunctionBundles()

        ' Build a fast lookup so bundled edges are not drawn a second time.
        Dim bundled As New HashSet(Of String)
        For Each b In bundles
            For Each src In b.Sources
                For Each tgt In b.Targets
                    bundled.Add($"{src.Id}_{tgt.Id}")
                Next
            Next
        Next

        ' Junction bundles first (under cards)
        For Each b In bundles
            DrawJunctionBundle(b)
        Next

        ' Remaining individual edges
        For Each sq In _sequences
            For Each outSq In sq.Outgoing
                If Not bundled.Contains($"{sq.Id}_{outSq.Id}") Then
                    DrawEdge(sq, outSq)
                End If
            Next
        Next

        ' Cards on top
        For Each sq In _sequences
            DrawSequenceCard(sq)
        Next

        ' Re-apply the current zoom/BaseScale to the freshly-built canvas —
        ' LayoutTransform is on GraphCanvas itself, which Clear() doesn't
        ' touch, but a fresh BuildAndRender should still reflect BaseScale
        ' even if no zoom interaction has happened yet.
        ApplyZoom(_zoom)
        UpdateFitAllAvailability()

    End Sub


    ' ── Junction-bundle detector ───────────────────────────────────────────────
    '  Groups source sequences by their outgoing-ID signature.
    '  Any group with ≥2 sources that share ≥2 identical targets becomes a bundle.

    Private Function FindJunctionBundles() As List(Of JunctionBundle)

        Dim result As New List(Of JunctionBundle)
        Dim groups As New Dictionary(Of String, List(Of SequenceNode))

        For Each sq In _sequences
            If sq.Outgoing.Count < 2 Then Continue For
            Dim sig = String.Join("|", sq.Outgoing.Select(Function(t) t.Id.ToString()) _
                                                   .OrderBy(Function(s) s))
            If Not groups.ContainsKey(sig) Then
                groups(sig) = New List(Of SequenceNode)
            End If
            groups(sig).Add(sq)
        Next

        For Each kvp In groups
            If kvp.Value.Count < 2 Then Continue For
            Dim sources = kvp.Value
            Dim targets = sources(0).Outgoing.OrderBy(Function(t) t.Y).ToList()

            Dim srcRight = sources.Max(Function(s) s.X + s.W)
            Dim tgtLeft = targets.Min(Function(t) t.X)
            Dim jx = (srcRight + tgtLeft) / 2.0

            Dim srcMidY = sources.Average(Function(s) s.Y + s.H / 2.0)
            Dim tgtMidY = targets.Average(Function(t) t.Y + t.H / 2.0)
            Dim jy = (srcMidY + tgtMidY) / 2.0

            result.Add(New JunctionBundle With {
                .Sources = sources,
                .Targets = targets,
                .Jx = jx,
                .Jy = jy
            })
        Next

        Return result

    End Function


    ' ── Junction-bundle renderer ───────────────────────────────────────────────
    '
    '  Draws M source-to-dot curves (no arrowhead) then N dot-to-target curves
    '  (with arrowhead), finishing with a filled junction dot on top.

    Private Sub DrawJunctionBundle(b As JunctionBundle)

        Dim stroke = GetBrush(130, 136, 176, 250)

        ' Source → junction (no arrowhead)

        For Each src In b.Sources
            Dim x1 = src.X + src.W + EDGE_GAP
            Dim y1 = src.Y + src.H / 2.0
            Dim dx = (b.Jx - x1) * 0.5
            Dim fig As New PathFigure With {.StartPoint = New Point(x1, y1)}
            fig.Segments.Add(New BezierSegment(
                New Point(x1 + dx, y1),
                New Point(b.Jx - dx, b.Jy),
                New Point(b.Jx, b.Jy), True))
            Dim geo As New PathGeometry()
            geo.Figures.Add(fig)
            GraphCanvas.Children.Add(New Path With {
                .Data = geo,
                .Stroke = stroke,
                .StrokeThickness = 1.8
            })
        Next

        ' Junction → target (with arrowhead).
        ' The bezier is shortened by the arrowhead depth so the drawn line ends
        ' exactly where the arrowhead wings start, giving a clean flush join.

        Dim arrowDepth = 9.0 * Math.Cos(Math.PI / 6.0)   ' sz * cos 30° ≈ 7.79

        For Each tgt In b.Targets
            Dim x2 = tgt.X - EDGE_GAP
            Dim y2 = tgt.Y + tgt.H / 2.0
            Dim lineX = x2 - arrowDepth          ' bezier ends here, arrow tip at x2
            Dim dx = (lineX - b.Jx) * 0.5
            Dim fig As New PathFigure With {.StartPoint = New Point(b.Jx, b.Jy)}
            fig.Segments.Add(New BezierSegment(
                New Point(b.Jx + dx, b.Jy),
                New Point(lineX - dx, y2),
                New Point(lineX, y2), True))
            Dim geo As New PathGeometry()
            geo.Figures.Add(fig)
            GraphCanvas.Children.Add(New Path With {
                .Data = geo,
                .Stroke = stroke,
                .StrokeThickness = 1.8
            })
            DrawArrowHead(x2, y2, 0.0)
        Next

        ' Junction dot – drawn last so it sits on top of all converging lines.

        Const DOT_DIAM As Double = 12.0
        Dim jDot As New Ellipse With {
            .Width = DOT_DIAM,
            .Height = DOT_DIAM,
            .Fill = GetBrush(80, 120, 195),
            .Stroke = New SolidColorBrush(Colors.WhiteSmoke),
            .StrokeThickness = 1.5,
            .ToolTip = "Common Intermediate"
        }
        Canvas.SetLeft(jDot, b.Jx - DOT_DIAM / 2.0)
        Canvas.SetTop(jDot, b.Jy - DOT_DIAM / 2.0)
        GraphCanvas.Children.Add(jDot)

    End Sub


    ' ── Cubic Bézier edge with arrowhead ──────────────────────────────────────
    '  Control points are set so the curve leaves horizontally from the right
    '  edge of the source card and arrives horizontally at the left edge of
    '  the target card, giving smooth S-curves for cross-column connections.

    Private Sub DrawEdge(fromSq As SequenceNode, toSq As SequenceNode)

        Dim x1 = fromSq.X + fromSq.W + EDGE_GAP
        Dim y1 = fromSq.Y + fromSq.H / 2.0
        Dim x2 = toSq.X - EDGE_GAP
        Dim y2 = toSq.Y + toSq.H / 2.0
        If x2 <= x1 + 1.0 Then Return   ' skip back-edges (layout artefact)

        Dim dx = (x2 - x1) * 0.5
        Dim seg As New BezierSegment(
            New Point(x1 + dx, y1),
            New Point(x2 - dx, y2),
            New Point(x2, y2), True)

        Dim fig As New PathFigure With {.StartPoint = New Point(x1, y1)}
        fig.Segments.Add(seg)
        Dim geometry As New PathGeometry()
        geometry.Figures.Add(fig)

        GraphCanvas.Children.Add(New Path With {
            .Data = geometry,
            .Stroke = GetBrush(130, 136, 176, 250),
            .StrokeThickness = 1.8
        })

        ' The tangent at t=1 of this bézier always points horizontally right
        ' because the last control point and endpoint share the same Y.
        DrawArrowHead(x2, y2, 0.0)

    End Sub

    Private Sub DrawArrowHead(tipX As Double, tipY As Double, angle As Double)

        Dim sz = 9.0
        Dim half = Math.PI / 6.0   ' 30°
        Dim lAng = Math.PI + angle + half
        Dim rAng = Math.PI + angle - half
        Dim p1 = New Point(tipX + sz * Math.Cos(lAng), tipY + sz * Math.Sin(lAng))
        Dim p2 = New Point(tipX + sz * Math.Cos(rAng), tipY + sz * Math.Sin(rAng))

        Dim fig As New PathFigure With {
            .StartPoint = New Point(tipX, tipY),
            .IsClosed = True
        }
        fig.Segments.Add(New LineSegment(p1, True))
        fig.Segments.Add(New LineSegment(p2, True))
        Dim geom As New PathGeometry()
        geom.Figures.Add(fig)

        GraphCanvas.Children.Add(New Path With {
            .Data = geom,
            .Fill = GetBrush(175, 156, 206, 255)
        })

    End Sub


    ' ── Sequence card ─────────────────────────────────────────────────────────
    '
    Private Sub DrawSequenceCard(seq As SequenceNode)

        ' ── Color palette ────────────────────────────────────────────────────

        Dim gradTop, gradBot, rim, hdrClr As Color

        If seq.ContainsSeed Then
            gradTop = GetColor(&HA4, &H84, &H10)
            gradBot = GetColor(&H60, &H4A, &H8)
            rim = GetColor(&HF4, &HD0, &H18)
            hdrClr = GetColor(88, &HF4, &HD0, &H18)
            seq.SequenceType = "Start"

        ElseIf seq.IsParallel Then
            gradTop = GetColor(&H11, &H63, &H77)
            gradBot = GetColor(&H8, &H35, &H43)
            rim = GetColor(&H18, &HA8, &HC8)
            hdrClr = GetColor(88, &H18, &HA8, &HC8)
            seq.SequenceType = "Alternative"

        ElseIf (seq.Incoming.Count > 1 AndAlso seq.Outgoing.Count > 1) Then
            gradTop = GetColor(&H5A, &H28, &H9B)
            gradBot = GetColor(&H3A, &H1C, &H5A)
            rim = GetColor(&HA0, &H50, &HE0)
            hdrClr = GetColor(88, &HA0, &H50, &HE0)
            seq.SequenceType = "Hub"

        ElseIf seq.IsTerminal Then
            gradTop = GetColor(&H9A, &H4E, &H14)
            gradBot = GetColor(&H5C, &H2D, &H8)
            rim = GetColor(&HDA, &H8E, &H2C)
            hdrClr = GetColor(88, &HDA, &H8E, &H2C)
            seq.SequenceType = "Terminal"

        Else
            gradTop = GetColor(&H2D, &H5C, &HC4)
            gradBot = GetColor(&H16, &H1F, &H78)
            rim = GetColor(&H52, &H84, &HE4)
            hdrClr = GetColor(88, &H52, &H84, &HE4)
            seq.SequenceType = "Linear"

        End If


        ' ── Card body ─────────────────────────────────────────────────────────

        Dim grad As New LinearGradientBrush With {
            .StartPoint = New Point(0.0, 0.0),
            .EndPoint = New Point(0.0, 1.0)
        }
        grad.GradientStops.Add(New GradientStop(gradTop, 0.0))
        grad.GradientStops.Add(New GradientStop(gradBot, 1.0))

        Dim rimBrush As New SolidColorBrush(rim)


        'display debug tooltip
        Dim card As New Border With {
            .Width = seq.W,
            .Height = seq.H,
            .Background = grad,
            .BorderBrush = rimBrush,
            .BorderThickness = New Thickness(1.5),
            .CornerRadius = New CornerRadius(8),
            .ToolTip = If(dlgConnectGraph.IsDebugMode, BuildToolTip(seq), Nothing),
            .Tag = rimBrush
        }

        ' ── Inner grid: header row + content row ─────────────────────────────────

        Dim contentGrid As New Grid()
        contentGrid.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(20)})
        contentGrid.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})

        Dim seedStr = If(seq.ContainsSeed, "➤ ", "")

        ' Header strip
        Dim headerBorder As New Border With {
            .CornerRadius = New CornerRadius(7, 7, 0, 0),
            .Background = New SolidColorBrush(hdrClr),
            .Padding = New Thickness(8, 0, 8, 3)
        }
        headerBorder.Child = New TextBlock With {
            .Text = $"{seedStr}Sequence {seq.Id + 1}",
            .Foreground = New SolidColorBrush(Colors.White),
            .FontSize = 13,
            .FontWeight = FontWeights.Bold,
            .VerticalAlignment = VerticalAlignment.Center,
            .HorizontalAlignment = HorizontalAlignment.Center
        }

        Grid.SetRow(headerBorder, 0)
        contentGrid.Children.Add(headerBorder)

        ' Object-count display
        Dim countPanel As New StackPanel With {
            .Orientation = Orientation.Horizontal,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(0, -1, 0, 0)
        }

        countPanel.Children.Add(New TextBlock With {
            .Text = seq.Members.Count.ToString(),
            .FontFamily = New FontFamily("Consolas"),
            .FontSize = 13,
            .FontWeight = FontWeights.Bold,
            .Margin = New Thickness(0, 0, 0, -2),
            .Foreground = New SolidColorBrush(Colors.White),
            .VerticalAlignment = VerticalAlignment.Center
        })
        countPanel.Children.Add(New TextBlock With {
            .Text = If(seq.Members.Count = 1, " Step", " Steps"),
            .FontSize = 11,
            .Margin = New Thickness(2, 0, 0, -1.5),
            .Foreground = GetBrush(180, 255, 255, 255),
            .VerticalAlignment = VerticalAlignment.Center
        })

        Grid.SetRow(countPanel, 1)
        contentGrid.Children.Add(countPanel)

        card.Cursor = Cursors.Hand

        AddHandler card.MouseEnter, Sub(s, ev) card.BorderBrush = New SolidColorBrush(Colors.Orange)
        AddHandler card.MouseLeave, Sub(s, ev) card.BorderBrush = If(ReferenceEquals(_selectedSeq, seq), CType(SelectedBorderBrush, Brush), CType(card.Tag, Brush))
        AddHandler card.MouseLeftButtonDown, Sub(s, ev)
                                                 SelectSequence(seq)
                                                 RaiseEvent SequenceCardClicked(seq)
                                             End Sub
        card.Child = contentGrid
        Canvas.SetLeft(card, seq.X)
        Canvas.SetTop(card, seq.Y)

        GraphCanvas.Children.Add(card)

        _seqCards(seq) = card

    End Sub


    ''' <summary>
    ''' Highlights the given sequence's card with a yellow border, marking it as
    ''' the current selection, and restores the previously selected card's
    ''' border to its normal color. Pass Nothing to clear the selection.
    ''' Safe to call both from a card's click handler and directly from
    ''' code-behind (e.g. to reflect a selection made elsewhere in the UI).
    ''' </summary>
    '''
    Public Sub SelectSequence(seq As SequenceNode)

        If ReferenceEquals(_selectedSeq, seq) Then Return

        Dim prevCard As Border = Nothing
        If _selectedSeq IsNot Nothing AndAlso _seqCards.TryGetValue(_selectedSeq, prevCard) Then
            prevCard.BorderBrush = CType(prevCard.Tag, Brush)
        End If

        _selectedSeq = seq

        Dim card As Border = Nothing
        If seq IsNot Nothing AndAlso _seqCards.TryGetValue(seq, card) Then
            card.BorderBrush = SelectedBorderBrush
        End If

    End Sub

    Public ReadOnly Property SelectedSequence As SequenceNode
        Get
            Return _selectedSeq
        End Get
    End Property

    ''' <summary>
    ''' Returns the accent (rim) color used for the given sequence's card, so
    ''' other parts of the UI (e.g. a title banner) can echo it.
    ''' </summary>
    '''
    Public Function GetSequenceAccentBrush(seq As SequenceNode) As Brush

        Dim card As Border = Nothing
        If seq IsNot Nothing AndAlso _seqCards.TryGetValue(seq, card) Then
            Return CType(card.Tag, Brush)
        End If

        Return Brushes.Gray

    End Function


    ' Tooltip shown on card hover 
    ' ----------------------------

    Private Function BuildToolTip(seq As SequenceNode) As ToolTip

        Dim sb As New StringBuilder()

        sb.AppendLine($"Sequence {seq.Id + 1}")
        sb.AppendLine(New String("─"c, 20))

        For Each node In seq.Members
            Dim conStr = $" ↓{node.Downstream.Count} ↑{node.Upstream.Count}"
            sb.AppendLine(node.ConnectItem.Title)
        Next
        sb.AppendLine()
        sb.Append($"In: {seq.Incoming.Count} - Out: {seq.Outgoing.Count}")

        Return New ToolTip With {
            .Background = GetBrush(12, 12, 30),
            .BorderBrush = GetBrush(52, 52, 100),
            .BorderThickness = New Thickness(1),
            .Padding = New Thickness(12, 9, 12, 9),
            .Content = New TextBlock With {
                .Text = sb.ToString(),
                .FontFamily = New FontFamily("Consolas"),
                .FontSize = 11,
                .Foreground = Brushes.White' GetBrush(120, 185, 185)
            }
        }
    End Function

End Class
