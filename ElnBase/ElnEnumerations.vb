Public Class ELNEnumerations

    ''' <summary>
    ''' Gets the value of the specified enum type T from the specified enum string.
    ''' </summary>
    ''' <typeparam name="T">The type of the enum</typeparam>
    ''' <param name="enumString">The enum string (case insensitive) of the specified enum type T to look for.</param>
    ''' <returns>The resulting enum type member. Raises an exception if the enum string is not found.</returns>
    ''' 
    Public Shared Function GetEnumVal(Of T)(enumString As String) As T

        'Intentionally does not catch the string not found exception
        'to provide direct developer feedback (this function is not 
        'intended for use in user edit interactions).

        Dim res = [Enum].Parse(GetType(T), enumString, True)
        Return res

    End Function


    ''' <summary>
    ''' Gets the MaterialUnitType of the specified unit enum string (case sensitive!)
    ''' </summary>
    ''' 
    Public Shared Function GetMaterialUnitType(enumString) As MaterialUnitType

        If [Enum].IsDefined(GetType(WeightUnit), enumString) Then
            Return MaterialUnitType.Weight

        ElseIf [Enum].IsDefined(GetType(VolumeUnit), enumString) Then
            Return MaterialUnitType.Volume

        ElseIf [Enum].IsDefined(GetType(EquivUnit), enumString) OrElse [Enum].IsDefined(GetType(EquivUnitShort), enumString) Then
            Return MaterialUnitType.Equivalent

        ElseIf [Enum].IsDefined(GetType(MolUnit), enumString) Then
            Return MaterialUnitType.Mol

        Else
            Return MaterialUnitType.Unknown

        End If

    End Function


    Public Enum MaterialType
        Reagent = 0
        Solvent = 1
        Auxiliary = 2
        Catalyst = 3
    End Enum


    Public Enum MaterialUnitType
        Weight
        Volume
        Equivalent
        Mol
        Unknown
    End Enum


    Public Enum WeightUnit
        mg
        g
        kg
    End Enum


    Public Enum VolumeUnit
        mL
        L
        µL
    End Enum


    Public Enum MolUnit
        mol
        mmol
        µmol
    End Enum


    Public Enum EquivUnit

        ''' <summary>Equivalent based on mmol reference reactant.</summary>
        equiv

        ''' <summary>Milli-equivalent based on total reference reactant mmols. Useful e.g. for catalysts.</summary>
        mEquiv

        ''' <summary>Volume equivalent based on total reference reactant grams.</summary>
        volEquiv

        ''' <summary>Volume equivalent based on total reference reactant mmols.</summary>
        molEquiv

        ''' <summary>Weight equivalent based on total reference reactant grams.</summary>
        wtEquiv

    End Enum


    Public Enum EquivUnitShort

        ''' <summary>Equivalent based on mmol reference reactant.</summary>
        eq

        ''' <summary>Milli-equivalent based on total reference reactant mmols. Useful e.g. for catalysts.</summary>
        mq

        ''' <summary>Volume equivalent based on total reference reactant grams.</summary>
        vq

        ''' <summary>Volume equivalent based on total reference reactant mmols.</summary>
        mv

        ''' <summary>Weight equivalent based on total reference reactant grams.</summary>
        wq

    End Enum


    Public Enum ProtocolElementType

        RefReactant
        Reagent
        Solvent
        Auxiliary
        Product
        Comment
        Image
        File
        Table
        Separator

    End Enum


    Public Enum RecalculationMode

        KeepEquivalents
        KeepAmounts
        KeepAsSpecified

    End Enum


    Public Enum UserTag

        NoTag
        Bookmarked
        Favorite

    End Enum


    Public Enum WorkflowStatus

        InProgress
        Finalized
        Unlocked

    End Enum


    Public Enum EmbeddedFileType

        Document
        Image

    End Enum


    Public Enum EmbeddingResult

        Succeeded
        UnsupportedType
        SizeExceeded
        TotalSizeExceeded
        FileNotFound
        NoImageContent

    End Enum


    Public Enum CloneType

        EmptyExperiment
        SketchOnly
        NextStepSketch
        FullExperiment
        FromImport

    End Enum


    Public Enum SyncResult

        Unprocessed
        Preprocessed
        Completed

    End Enum


    Public Enum MaterialValidation
        None
        Preset
        User
    End Enum

End Class
