Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Public Class ELNCalculations

    ''' <summary>
    ''' Recalculates the amounts of all non-reference reactant materials in relation to the current 
    ''' total amount of reference reactant present.
    ''' </summary>
    ''' <param name="expEntry">The experiment entry to recalculate.</param>
    ''' <param name="recalcMode">The RecalculationMode to apply.</param>
    ''' 
    Public Shared Sub RecalculateMaterials(expEntry As tblExperiments, recalcMode As RecalculationMode)

        'update refReactant totals as the basis for the subsequent calculations
        With expEntry
            .RefReactantGrams = GetTotalRefReactantGrams(expEntry)
            .RefReactantMMols = GetTotalRefReactantMmols(expEntry)
        End With

        If expEntry.RefReactantGrams > 0 Then

            For Each protItem In expEntry.tblProtocolItems
                With protItem
                    Select Case .ElementType

                        Case ProtocolElementType.Reagent

                            Dim prevSpecified As MaterialUnitType = .tblReagents.SpecifiedUnitType

                            Select Case recalcMode
                                Case RecalculationMode.KeepAmounts
                                    .tblReagents.SpecifiedUnitType = MaterialUnitType.Weight
                                Case RecalculationMode.KeepEquivalents
                                    .tblReagents.SpecifiedUnitType = MaterialUnitType.Equivalent
                                Case RecalculationMode.KeepAsSpecified
                                    'do nothing, keep as originally specified
                            End Select

                            RecalculateReagent(.tblReagents)
                            .tblReagents.SpecifiedUnitType = prevSpecified  'reset original value


                        Case ProtocolElementType.Solvent

                            Dim prevSpecified As MaterialUnitType = .tblSolvents.SpecifiedUnitType

                            Select Case recalcMode
                                Case RecalculationMode.KeepAmounts
                                    .tblSolvents.SpecifiedUnitType = MaterialUnitType.Volume
                                Case RecalculationMode.KeepEquivalents
                                    .tblSolvents.SpecifiedUnitType = MaterialUnitType.Equivalent
                                Case RecalculationMode.KeepAsSpecified
                                    'do nothing, keep as originally specified
                            End Select

                            RecalculateSolvent(.tblSolvents)
                            .tblSolvents.SpecifiedUnitType = prevSpecified  'reset original value


                        Case ProtocolElementType.Auxiliary

                            Dim prevSpecified As MaterialUnitType = .tblAuxiliaries.SpecifiedUnitType

                            Select Case recalcMode
                                Case RecalculationMode.KeepAmounts
                                    .tblAuxiliaries.SpecifiedUnitType = MaterialUnitType.Weight
                                Case RecalculationMode.KeepEquivalents
                                    .tblAuxiliaries.SpecifiedUnitType = MaterialUnitType.Equivalent
                                Case RecalculationMode.KeepAsSpecified
                                    'do nothing, keep as originally specified
                            End Select

                            RecalculateAuxiliary(.tblAuxiliaries)
                            .tblAuxiliaries.SpecifiedUnitType = prevSpecified  'reset original value

                        Case ProtocolElementType.Product

                            RecalculateProduct(.tblProducts)

                    End Select
                End With
            Next

            'update experiment yield
            Dim refYield = GetTotalProductYield(expEntry, 0)
            expEntry.Yield = If(refYield = 0, Nothing, refYield)

        End If

    End Sub


    ''' <summary>
    ''' Updates the reference reactant amounts and the yield of the specified experiment.
    ''' </summary>
    ''' <param name="expEntry">The experiment entry to update.</param>
    ''' 
    Public Shared Sub UpdateExperimentTotals(expEntry As tblExperiments)

        With expEntry

            With expEntry
                .RefReactantGrams = ELNCalculations.GetTotalRefReactantGrams(expEntry)
                .RefReactantMMols = ELNCalculations.GetTotalRefReactantMmols(expEntry)
            End With

            Dim refYield = ELNCalculations.GetTotalProductYield(expEntry, 0)
            expEntry.Yield = If(refYield = 0, Nothing, refYield)

        End With

    End Sub


    ''' <summary>
    ''' Gets the total grams of reference reactant present in the experiment, also if present 
    ''' in multiple portions.
    ''' </summary>
    ''' <returns>Total grams of reference reactant.</returns>
    ''' 
    Public Shared Function GetTotalRefReactantGrams(expEntry As tblExperiments) As Double

        Return Aggregate prot In expEntry.tblProtocolItems Where prot.ElementType = ProtocolElementType.RefReactant
                  Into Sum(prot.tblRefReactants.Grams)

    End Function


    ''' <summary>
    ''' Gets the total mmols of reference reactant present in the experiment, also if present 
    ''' in multiple portions.
    ''' </summary>
    ''' <returns>Total mmols of reference reactant.</returns>
    ''' 
    Public Shared Function GetTotalRefReactantMmols(expEntry As tblExperiments) As Double

        Return Aggregate protItem In expEntry.tblProtocolItems Where protItem.ElementType = ProtocolElementType.RefReactant
                  Into Sum(protItem.tblRefReactants.MMols)

    End Function


    ''' <summary>
    ''' Recalculates the specified RefReactant grams, equivalents and mmols based on the current SpecifiedUnitType.
    ''' </summary>
    ''' 
    Public Shared Sub RecalculateRefReactant(refReactEntry As tblRefReactants)

        With refReactEntry

            Dim molweight = .MolecularWeight
            Dim exp = .ProtocolItem.Experiment
            Dim purity = If(IsNothing(.Purity), 1, .Purity / 100.0)

            'for correct equivalents calc when multiple portions of refReactant present.
            Dim otherMMolsPresent = Aggregate prot In exp.tblProtocolItems Where prot.ElementType = ProtocolElementType.RefReactant AndAlso
                               prot.tblRefReactants.GUID <> refReactEntry.GUID
                               Into Sum(prot.tblRefReactants.MMols)

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Weight

                    If .ResinLoad Is Nothing Then
                        .MMols = purity * 1000.0 * .Grams / molweight
                    Else
                        .MMols = purity * .Grams * .ResinLoad
                    End If
                    .Equivalents = .MMols / (.MMols + otherMMolsPresent)

                Case MaterialUnitType.Mol

                    If .ResinLoad Is Nothing Then
                        .Grams = .MMols * molweight / purity / 1000.0
                    Else
                        .Grams = .MMols / .ResinLoad / purity
                    End If
                    .Equivalents = .MMols / (.MMols + otherMMolsPresent)

            End Select

        End With

    End Sub


    ''' <summary>
    ''' Recalculates the specified Reagent grams, equivalents and mmols based on the current SpecifiedUnitType.
    ''' </summary>
    ''' 
    Public Shared Sub RecalculateReagent(reagentEntry As tblReagents)

        With reagentEntry

            Dim molweight = .MolecularWeight
            Dim refmMols = .ProtocolItem.Experiment.RefReactantMMols
            Dim purity = If(IsNothing(.Purity), 1, .Purity / 100.0)

            If .IsMolarity = 0 Then 'non-molar reagent

                Select Case .SpecifiedUnitType

                    Case MaterialUnitType.Equivalent
                        .MMols = .Equivalents * refmMols
                        If .ResinLoad Is Nothing Then
                            .Grams = .MMols * molweight / 1000.0 / purity
                        Else
                            .Grams = .MMols / .ResinLoad / purity
                        End If

                    Case MaterialUnitType.Weight
                        If .ResinLoad Is Nothing Then
                            .MMols = purity * 1000.0 * .Grams / molweight
                        Else
                            .MMols = purity * .Grams * .ResinLoad
                        End If
                        .Equivalents = .MMols / refmMols

                End Select

            Else   'molar solution

                Select Case .SpecifiedUnitType

                    Case MaterialUnitType.Equivalent
                        .MMols = .Equivalents * refmMols
                        .Grams = .MMols / .Molarity / purity   'Grams property actually contains *mL* for molar sln.

                    Case MaterialUnitType.Volume
                        .MMols = purity * .Grams * .Molarity
                        .Equivalents = .MMols / refmMols

                End Select

            End If

        End With

    End Sub


    ''' <summary>
    ''' Recalculates the specified Solvent milliliters, equivalents and mmols based on the current SpecifiedUnitType.
    ''' </summary>
    ''' 
    Public Shared Sub RecalculateSolvent(solventEntry As tblSolvents)

        With solventEntry

            Dim refmMols = .ProtocolItem.Experiment.RefReactantMMols
            Dim refGrams = .ProtocolItem.Experiment.RefReactantGrams

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Volume
                    If .IsMolEquivalents = 0 Then
                        .Equivalents = .Milliliters / refGrams
                    Else
                        .Equivalents = .Milliliters / refmMols
                    End If

                Case MaterialUnitType.Equivalent
                    If .IsMolEquivalents = 0 Then
                        .Milliliters = .Equivalents * refGrams
                    Else
                        .Milliliters = .Equivalents * refmMols
                    End If

            End Select

        End With

    End Sub



    Public Shared Sub RecalculateAuxiliary(auxEntry As tblAuxiliaries)

        With auxEntry

            Dim refGrams = .ProtocolItem.Experiment.RefReactantGrams

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Equivalent    'weight equivalents
                    .Grams = refGrams * .Equivalents

                Case MaterialUnitType.Weight
                    .Equivalents = .Grams / refGrams

            End Select

        End With

    End Sub


    Public Shared Sub RecalculateProduct(prodEntry As tblProducts)

        If prodEntry IsNot Nothing Then     'product placeholders are nothing

            With prodEntry

                Dim purity = If(IsNothing(.Purity), 1, .Purity / 100)
                Dim refMmols = .ProtocolItem.Experiment.RefReactantMMols
                Dim prodMmols As Double

                If .ResinLoad Is Nothing Then
                    prodMmols = purity * 1000.0 * .Grams / .MolecularWeight
                Else
                    prodMmols = purity * .ResinLoad * .Grams
                End If

                Dim yieldFactor = If(.ProductIndex = 0, .ProtocolItem.Experiment.RefYieldFactor, 1)  'only reference product is affected
                .Yield = 100.0 * prodMmols * yieldFactor / refMmols

            End With

        End If

    End Sub


    ''' <summary>
    ''' Gets the total yield of the product with the specified productIndex.
    ''' </summary>
    ''' <param name="expEntry"></param>
    ''' <param name="productIndex">Index reflects the order of product appearance in the reaction 
    ''' sketch (0 is reference product).</param>/>
    ''' <returns>Returns nothing if no yield present.</returns>
    ''' 
    Public Shared Function GetTotalProductYield(expEntry As tblExperiments, productIndex As Integer) As Double?

        'Note: ProtocolItem.tblProducts is nothing for cloning product placeholders.

        Return Aggregate prot In expEntry.tblProtocolItems Where prot.ElementType = ProtocolElementType.Product AndAlso
            prot.tblProducts IsNot Nothing AndAlso prot.tblProducts.ProductIndex = productIndex Into
            Sum(prot.tblProducts.Yield)

    End Function


    Public Class ScaleResult

        Public Property Amount As Double
        Public Property Unit As String

    End Class


    ''' <summary>
    ''' Gets the fitting weight amount/unit pair resulting from the specified gram value.
    ''' </summary>
    ''' 
    Public Shared Function ScaleWeight(grams As Double) As ScaleResult

        Dim res As New ScaleResult

        Select Case grams
            Case 0
                'i.e. adding new
                Return Nothing

            Case >= 1000.0
                res.Amount = grams / 1000.0
                res.Unit = WeightUnit.kg.ToString

            Case >= 1
                res.Amount = grams
                res.Unit = WeightUnit.g.ToString

            Case Else
                res.Amount = grams * 1000.0
                res.Unit = WeightUnit.mg.ToString

        End Select

        Return res

    End Function


    ''' <summary>
    ''' Gets the fitting weight amount/unit pair resulting from the specified milliliter value.
    ''' </summary>
    ''' 
    Public Shared Function ScaleVolume(milliliters As Double) As ScaleResult

        Dim res As New ScaleResult

        Select Case milliliters
            Case 0
                'i.e. adding new
                Return Nothing

            Case >= 1000.0
                res.Amount = milliliters / 1000.0
                res.Unit = VolumeUnit.L.ToString

            Case >= 1
                res.Amount = milliliters
                res.Unit = VolumeUnit.mL.ToString

            Case Else
                res.Amount = milliliters * 1000.0
                res.Unit = VolumeUnit.µL.ToString

        End Select

        Return res

    End Function


    ''' <summary>
    ''' Gets the fitting equivalent amount/unit pair resulting from the specified equivalent value.
    ''' </summary>
    ''' <param name="equivalents">The equivalents to convert.</param>
    ''' <param name="isShortUnit">If true, the unit is returned as equiv shortunit, e.g. 'eq' instead of 'equiv".</param>
    ''' 
    Public Shared Function ScaleEquivalent(equivalents As Double, isShortUnit As Boolean) As ScaleResult

        Dim res As New ScaleResult

        Select Case equivalents
            Case 0
                'when adding new
                Return Nothing

            Case >= 0.1
                res.Amount = equivalents
                If Not isShortUnit Then
                    res.Unit = EquivUnit.equiv.ToString
                Else
                    res.Unit = EquivUnitShort.eq.ToString
                End If

            Case Else
                res.Amount = equivalents * 1000.0
                If Not isShortUnit Then
                    res.Unit = EquivUnit.mEquiv.ToString
                Else
                    res.Unit = EquivUnitShort.mq.ToString
                End If

        End Select

        Return res

    End Function


    ''' <summary>
    ''' Gets the fitting weight amount/unit pair resulting from the specified milliliter value.
    ''' </summary>
    ''' 
    Public Shared Function ScaleMMol(mmols As Double) As ScaleResult

        Dim res As New ScaleResult

        Select Case mmols
            Case 0
                'when adding new
                Return Nothing

            Case >= 1000.0
                res.Amount = mmols / 1000.0
                res.Unit = MolUnit.mol.ToString

            Case >= 1
                res.Amount = mmols
                res.Unit = MolUnit.mmol.ToString

            Case Else
                res.Amount = mmols * 1000.0
                res.Unit = MolUnit.µmol.ToString

        End Select

        Return res

    End Function



    ''' <summary>
    ''' Converts the specified volume to grams.
    ''' </summary>
    ''' <param name="vol">The volume amount.</param>
    ''' <param name="unit">The associated VolumeUnit</param>
    ''' <param name="density">The material density.</param>
    ''' <returns>The amount in grams, or nothing if an error occurred.</returns>
    ''' 
    Public Shared Function ConvertVolToGrams(vol As Double, unit As VolumeUnit, density As Double) As Double?

        'density required for volume conversion

        If density <= 0 Then
            Return Nothing
        End If

        Select Case unit

            'liquid units
            Case VolumeUnit.L
                Return vol * density * 1000.0
            Case VolumeUnit.mL
                Return vol * density
            Case VolumeUnit.µL
                Return vol * density / 1000.0
            Case Else
                Return Nothing

        End Select

    End Function


    ''' <summary>
    ''' Converts the specified amount and WeightUnit pair into grams.
    ''' </summary>
    ''' <param name="amount">The material amount</param>
    ''' <param name="unit">The associated WeightUnit.</param>
    ''' <returns>The amount in grams, or nothing if an error occurred.</returns>
    ''' 
    Public Shared Function ConvertToGrams(amount As Double, unit As WeightUnit) As Double?

        Select Case unit

            Case WeightUnit.kg
                Return amount * 1000.0
            Case WeightUnit.g
                Return amount
            Case WeightUnit.mg
                Return amount / 1000.0
            Case Else
                Return Nothing

        End Select

    End Function


    ''' <summary>
    ''' Converts the specified amount and VolumeUnit pair into milliliters.
    ''' </summary>
    ''' <param name="amount">The volume amount.</param>
    ''' <param name="volUnit">The associated VolumeUnit.</param>
    ''' <returns>The amount in milliliters, or nothing if an error occurred.</returns>
    ''' 
    Public Shared Function ConvertToML(amount As Double, volUnit As VolumeUnit) As Double?

        Select Case volUnit
            Case VolumeUnit.L
                Return 1000.0 * amount
            Case VolumeUnit.mL
                Return amount
            Case VolumeUnit.µL
                Return amount / 1000.0
            Case Else
                Return Nothing
        End Select

    End Function


    ''' <summary>
    ''' Converts the specified amount and MolUnit pair into mmols.
    ''' </summary>
    ''' <param name="amount">The mmol amount.</param>
    ''' <param name="volUnit">The associated MolUnit.</param>
    ''' <returns>The amount in mmols, or nothing if an error occurred.</returns>
    ''' 
    Public Shared Function ConvertToMMol(amount As Double, molUnit As MolUnit) As Double?

        Select Case molUnit
            Case MolUnit.mol
                Return 1000.0 * amount
            Case MolUnit.mmol
                Return amount
            Case MolUnit.µmol
                Return amount / 1000.0
            Case Else
                Return Nothing
        End Select

    End Function


    ''' <summary>
    ''' Converts the specified amount and EquivUnit pair into equivalents.
    ''' </summary>
    ''' <param name="amount">The mmol amount.</param>
    ''' <param name="equivUnit">The associated EquivUnitShort.</param>
    ''' <returns>The amount in equivalents, or nothing if an error occurred.</returns>
    ''' 
    Public Shared Function ConvertToEquiv(equivAmount As Double, equivUnit As EquivUnitShort) As Double?

        Select Case equivUnit
            Case EquivUnitShort.eq, EquivUnitShort.wq, EquivUnitShort.vq, EquivUnitShort.mv
                Return equivAmount
            Case EquivUnitShort.mq
                Return equivAmount / 1000.0
            Case Else
                Return Nothing
        End Select

    End Function


    ''' <summary>
    ''' Converts the specified weight unit string into a WeightUnit.
    ''' </summary>
    ''' <param name="unit">The weight unit as string. The assignment is case insensitive.</param>
    ''' <returns>A WeightUnit, or nothing if the string could not be assigned.</returns>
    ''' 
    Public Shared Function ToWeightUnit(unit As String) As WeightUnit

        unit = unit.ToLower

        Select Case unit
            Case WeightUnit.g.ToString.ToLower
                Return WeightUnit.g
            Case WeightUnit.kg.ToString.ToLower
                Return WeightUnit.kg
            Case WeightUnit.mg.ToString.ToLower
                Return WeightUnit.mg
            Case Else
                Return Nothing
        End Select

    End Function


    ''' <summary>
    ''' Converts the specified volume unit string into a VolumeUnit.
    ''' </summary>
    ''' <param name="unit">The volume unit as string. The assignment is case insensitive.</param>
    ''' <returns>A VolumeUnit, or nothing if the string could not be assigned.</returns>
    ''' 
    Public Shared Function ToVolumeUnit(unit As String) As VolumeUnit

        unit = unit.ToLower

        Select Case unit
            Case VolumeUnit.mL.ToString.ToLower
                Return VolumeUnit.mL
            Case VolumeUnit.L.ToString.ToLower
                Return VolumeUnit.L
            Case VolumeUnit.µL.ToString.ToLower
                Return VolumeUnit.µL
            Case Else
                Return Nothing
        End Select

    End Function


    ''' <summary>
    ''' Converts the specified mol unit string into a MolUnit.
    ''' </summary>
    ''' <param name="unit">The mol unit as string. The assignment is case insensitive.</param>
    ''' <returns>A MolUnit, or nothing if the string could not be assigned.</returns>
    ''' 
    Public Shared Function ToMolUnit(unit As String) As MolUnit

        unit = unit.ToLower

        Select Case unit
            Case MolUnit.mol.ToString.ToLower
                Return MolUnit.mol
            Case MolUnit.mmol.ToString.ToLower
                Return MolUnit.mmol
            Case MolUnit.µmol.ToString.ToLower
                Return MolUnit.µmol
            Case Else
                Return Nothing
        End Select

    End Function


    ''' <summary>
    ''' Converts the specified equivalent unit string into an EquivUnit.
    ''' </summary>
    ''' <param name="unit">The equiv unit as string. The assignment is case insensitive.</param>
    ''' <returns>An EquivUnit, or nothing if the string could not be assigned.</returns>
    ''' 
    Public Shared Function ToEquivUnit(unit As String) As EquivUnit

        unit = unit.ToLower

        Select Case unit
            Case EquivUnit.equiv.ToString.ToLower
                Return EquivUnit.equiv
            Case EquivUnit.mEquiv.ToString.ToLower
                Return EquivUnit.mEquiv
            Case Else
                Return Nothing
        End Select

    End Function



    ''' <summary>
    ''' Converts a double value into a string with the specified number of significant digits. 
    ''' </summary>
    ''' <param name="val">The double value to convert.</param>
    ''' <param name="sigDigits">The number of desired significant digits.</param>
    ''' <returns>
    ''' A string of the specified value. If the absolute value of val is smaller than 0.001, then the 
    ''' string 'smaller than 0.001' is returned. If the integer part digit count of val is larger than sigDigits, 
    ''' then all digits except the fractional part are returned.
    ''' </returns>
    ''' 
    Public Shared Function SignificantDigitsString(ByVal val As Double, ByVal sigDigits As Integer) As String

        Dim currDecSeparator As String = Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator().ToString
        Dim upperLimit As Integer = Math.Pow(10, sigDigits)
        Dim absVal = Math.Abs(val)

        Select Case absVal

            'very small numbers: gradually reduce significant digits

            Case < 0.001
                If val >= 0 Then
                    Return "< 0.001"
                Else
                    Return "< -0.001"
                End If

            Case < 0.01
                Return Format(val, "G1")

            Case < 0.1
                Return Format(val, "G2")

            Case < upperLimit

                'sig dig range without scientific exponentials of "G" string format

                Dim valueStr = Format(val, "G" + sigDigits.ToString)
                Dim absStr = valueStr.Replace("-", "")  'remove negative sign for digit count
                absStr = absStr.Replace(".", "")        'remove decimal separator for digit count
                absStr = absStr.TrimStart("0")          'remove leading zeros

                If absStr.Length < sigDigits AndAlso absVal >= 1 Then

                    Dim sep = If(valueStr.Contains(currDecSeparator), "", currDecSeparator)

                    'replace trailing zeroes removed by format "G"
                    If CInt(absStr) < 10 Then
                        valueStr += sep + "00"
                    Else
                        valueStr += sep + "0"
                    End If

                End If

                Return valueStr

            Case Else

                'display larger values with all digits except omitted decimals, and thousands separator
                Return Format(val, "N0")

        End Select

    End Function

End Class
