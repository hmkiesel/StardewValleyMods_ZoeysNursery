// Credit to Atra (https://www.github.com/atravita-mods)
// This is a dumbed down and simplified version of their ILHelper util class.
// original: https://github.com/atravita-mods/StardewMods/blob/main/AtraShared/Utils/HarmonyHelper/ILHelper.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using StardewModdingAPI;


namespace HarmonyUtils;

/// <summary>
/// Helper class for transpilers.
/// </summary>
public sealed class TranspilerHelper
{
    // All locals.
    private readonly SortedList<int, LocalVariableInfo> locals = new();

    private readonly Dictionary<Label, int> importantLabels = new();

    private Label? label = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranspilerHelper"/> class.
    /// </summary>
    /// <param name="original">MethodBase for original method</param>
    /// <param name="codes">IEnumerable of CodeInstruction instructions</param>
    /// <param name="monitor">Logger</param>
    /// <param name="generator">ILGenerator for generating MSIL instructions</param>
    public TranspilerHelper(MethodBase original, IEnumerable<CodeInstruction> codes, IMonitor monitor, ILGenerator generator)
    {
        // scan method body and get the original variables.
        if (original.GetMethodBody() is MethodBody body)
        {
            foreach (LocalVariableInfo loc in body.LocalVariables)
            {
                this.locals[loc.LocalIndex] = loc;
            }
        }
        else
        {
            throw new InvalidOperationException($"Attempted to transpile a method without a body: {original.FullDescription()}");
        }

        this.Original = original;
        this.Codes = codes.ToList();
        this.Generator = generator;
        this.Monitor = monitor;

        // update the variables with the ones mods have added
        // and also take count of the labels.
        foreach (CodeInstruction code in this.Codes)
        {
            if (code.operand is LocalBuilder builder)
            {
                this.locals.TryAdd(builder.LocalIndex, builder); // LocalBuilder inherits from LocalVariableInfo
            }
            if (code.Branches(out Label? label))
            {
                if (this.importantLabels.ContainsKey(label!.Value))
                {
                    this.importantLabels[label!.Value]++;
                }
                else
                {
                    this.importantLabels.Add(label!.Value, 1);
                }
            }
            if (code.opcode == OpCodes.Switch)
            {
                foreach (Label switchLabel in (Label[])code.operand)
                {
                    if (this.importantLabels.ContainsKey(switchLabel))
                    {
                        this.importantLabels[switchLabel]++;
                    }
                    else
                    {
                        this.importantLabels.Add(switchLabel, 1);
                    }
                }
            }
        }
    }

    #region properties

    /// <summary>
    /// Gets the original MethodBase.
    /// </summary>
    public MethodBase Original { get; init; }

    /// <summary>
    /// Gets the list of codes.
    /// </summary>
    /// <remarks>Try not to use this.</remarks>
    public List<CodeInstruction> Codes { get; init; }

    /// <summary>
    /// Gets the ILGenerator.
    /// </summary>
    public ILGenerator Generator { get; init; }

    /// <summary>
    /// Gets the current instruction pointer stack.
    /// </summary>
    public Stack<int> PointerStack { get; private set; } = new();

    /// <summary>
    /// Points to the current location in the instructions list.
    /// </summary>
    public int Pointer { get; private set; } = -1;

    /// <summary>
    /// Gets the current instruction.
    /// </summary>
    public CodeInstruction CurrentInstruction
    {
        get => this.Codes[this.Pointer];
        private set => this.Codes[this.Pointer] = value;
    }

    /// <summary>
    /// Gets the logger for this instance.
    /// </summary>
    private IMonitor Monitor { get; init; }

    #endregion


    #region jumps

    /// <summary>
    /// Pushes the pointer onto the pointer stack.
    /// </summary>
    /// <returns>this.</returns>
    public TranspilerHelper Push()
    {
        this.PointerStack.Push(this.Pointer);
        return this;
    }

    /// <summary>
    /// Pops the a pointer from the pointer stack.
    /// </summary>
    /// <returns>this.</returns>
    public TranspilerHelper Pop()
    {
        this.Pointer = this.PointerStack.Pop();
        return this;
    }

    /// <summary>
    /// Moves the pointer to a specific index.
    /// </summary>
    /// <param name="index">Index to move to.</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Tried to move to an invalid location.</exception>
    public TranspilerHelper JumpTo(int index)
    {
        if (index < 0 || index > this.Codes.Count - 1)
        {
            throw new IndexOutOfRangeException($"index ({index}) is out of range (0 - {this.Codes.Count - 1}");
        }
        this.Pointer = index;
        return this;
    }

    /// <summary>
    /// Moves the pointer forward the number of steps.
    /// </summary>
    /// <param name="steps">Number of steps.</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Pointer tried to move to an invalid location.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TranspilerHelper Advance(int steps)
    {
        this.Pointer += steps;
        if (this.Pointer > this.Codes.Count - 1)
        {
            throw new IndexOutOfRangeException($"pointer index ({this.Pointer}) was advanced out of range (0 - {this.Codes.Count - 1}");
        }
        return this;
    }

    #endregion

    /// <summary>
    /// The list of codes as an enumerable.
    /// </summary>
    /// <returns>Returns the list of codes as an enumerable.</returns>
    public IEnumerable<CodeInstruction> Render() =>
        this.Codes.AsEnumerable();

    /// <summary>
    /// Prints out the current codes to console.
    /// Only works in DEBUG.
    /// </summary>
    [DebuggerHidden]
    [Conditional("DEBUG")]
    public void Print()
    {
        StringBuilder sb = new();
        sb.Append("TranspilerHelper for: ").AppendLine(this.Original.FullDescription());
        sb.Append("With locals: ").AppendJoin(", ", this.locals.Values.Select((LocalVariableInfo loc) => $"{loc.LocalIndex}+{loc.LocalType.Name}"));
        for (int i = 0; i < this.Codes.Count; i++)
        {
            sb.AppendLine().Append(this.Codes[i]);
            if (this.Pointer == i)
            {
                sb.Append("       <----");
            }
            if (this.PointerStack.Contains(i))
            {
                sb.Append("       <----- stack point.");
            }
        }
        this.Monitor.Log(sb.ToString(), LogLevel.Info);
    }

    #region search

    /// <summary>
    /// Finds the first occurrence of the following pattern between the indexes given.
    /// </summary>
    /// <param name="instructions">Instructions to search for.</param>
    /// <param name="startindex">Index to start searching at (inclusive).</param>
    /// <param name="intendedendindex">Index to end search (exclusive). Null for "end of instruction list".</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentException">StartIndex or EndIndex are invalid.</exception>
    /// <exception cref="InvalidOperationException">No match found.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TranspilerHelper FindFirst(CodeInstruction[] instructions, int startindex = 0, int? intendedendindex = null)
    {
        if (instructions.Length == 0)
        {
            throw new ArgumentException("no instructions available");
        }

        int endindex = intendedendindex ?? this.Codes.Count;
        if (startindex >= (endindex - instructions.Length) || startindex < 0 || endindex > this.Codes.Count)
        {
            throw new ArgumentException($"Either the start index {startindex} or the end index {endindex} are invalid.");
        }

        for (int i = startindex; i < endindex - instructions.Length + 1; i++)
        {
            for (int j = 0; j < instructions.Length; j++)
            {
                if (!this.IsMatch(instructions[j], this.Codes[i + j]))
                {
                    goto ContinueSearchForward;
                }
            }
            this.Pointer = i;
            return this;
        ContinueSearchForward:
            ;
        }
        throw new InvalidOperationException("could not find first occurrence of pattern: \n" + instructions.Select(instruction => instruction.ToString()).ToString());
    }

    /// <summary>
    /// Finds the first occurrence of the following pattern between the indexes given.
    /// </summary>
    /// <param name="instruction">Instruction to search for.</param>
    /// <param name="startindex">Index to start searching at (inclusive).</param>
    /// <param name="intendedendindex">Index to end search (exclusive). Null for "end of instruction list".</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentException">StartIndex or EndIndex are invalid.</exception>
    /// <exception cref="InvalidOperationException">No match found.</exception>
    public TranspilerHelper FindFirst(CodeInstruction instruction, int startindex = 0, int? intendedendindex = null)
    {
        int endindex = intendedendindex ?? this.Codes.Count;
        if (startindex >= (endindex - 1) || startindex < 0 || endindex > this.Codes.Count)
        {
            throw new ArgumentException($"Either the start index {startindex} or the end index {endindex} are invalid.");
        }
        for (int i = startindex; i < endindex; i++)
        {
            if (this.IsMatch(instruction, this.Codes[i]))
            {
                this.Pointer = i;
                return this;
            }
        }
        throw new InvalidOperationException($"The desired pattern wasn't found: {instruction}");
    }

    /// <summary>
    /// Finds the next occurrence of the code instruction.
    /// </summary>
    /// <param name="instructions">Instructions to search for.</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentException">Fewer codes remain than the length of the instructions to search for.</exception>
    /// <exception cref="InvalidOperationException">No match found.</exception>
    public TranspilerHelper FindNext(CodeInstruction[] instructions)
        => this.FindFirst(instructions, this.Pointer + 1, this.Codes.Count);

    /// <summary>
    /// Finds the last occurrence of the following pattern between the indexes given.
    /// </summary>
    /// <param name="instructions">Instructions to search for.</param>
    /// <param name="startindex">Index to start searching at (inclusive).</param>
    /// <param name="intendedendindex">Index to end search (exclusive). Leave null to mean "last code".</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentException">StartIndex or EndIndex are invalid.</exception>
    /// <exception cref="InvalidOperationException">No match found.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TranspilerHelper FindLast(CodeInstruction[] instructions, int startindex = 0, int? intendedendindex = null)
    {
        if (instructions.Length == 0)
        {
            throw new ArgumentException("no instructions available");
        }

        int endindex = intendedendindex ?? this.Codes.Count;
        if (startindex >= endindex - instructions.Length || startindex < 0 || endindex > this.Codes.Count)
        {
            throw new ArgumentException($"Either {nameof(startindex)} {startindex} or {nameof(endindex)} {endindex} are invalid. ");
        }
        for (int i = endindex - instructions.Length - 1; i >= startindex; i--)
        {
            for (int j = 0; j < instructions.Length; j++)
            {
                if (!this.IsMatch(instructions[j], this.Codes[i + j]))
                {
                    goto ContinueSearchBackwards;
                }
            }
            this.Pointer = i;
            return this;
            ContinueSearchBackwards:;
        }

        throw new InvalidOperationException("could not find last occurrence of pattern: \n" + instructions.Select(instruction => instruction.ToString()).ToString());
    }

    /// <summary>
    /// Finds the previous occurrence of the code instruction.
    /// </summary>
    /// <param name="instructions">Instructions to search for.</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentException">Fewer codes remain than the length of the instructions to search for.</exception>
    /// <exception cref="InvalidOperationException">No match found.</exception>
    public TranspilerHelper FindPrev(CodeInstruction[] instructions)
        => this.FindLast(instructions, 0, this.Pointer);

    #endregion

    #region manipulation

    /// <summary>
    /// Inserts the following code instructions at this location.
    /// </summary>
    /// <param name="instructions">Instructions to insert.</param>
    /// <param name="withLabels">Labels to attach to the first instruction.</param>
    /// <returns>this.</returns>
    public TranspilerHelper Insert(CodeInstruction[] instructions, IList<Label> withLabels = null)
    {
        this.Codes.InsertRange(this.Pointer, instructions);
        if (withLabels is not null)
        {
            this.CurrentInstruction.labels.AddRange(withLabels);
        }
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Branches(out Label? label))
            {
                if (this.importantLabels.ContainsKey(label!.Value))
                {
                    this.importantLabels[label!.Value]++;
                }
                else
                {
                    this.importantLabels.Add(label!.Value, 1);
                }
            }
        }
        this.Pointer += instructions.Length;
        return this;
    }

    #endregion

    #region labels

    /// <summary>
    /// Grab branch destination.
    /// </summary>
    /// <param name="label">Label the instruction branches to.</param>
    /// <returns>this.</returns>
    /// <exception cref="InvalidOperationException">Current instruction does not branch.</exception>
    public TranspilerHelper GrabBranchDest(out Label? label)
    {
        if (!this.CurrentInstruction.Branches(out label))
        {
            throw new InvalidOperationException($"Attempted to grab label from something that's not a branch.");
        }
        return this;
    }

    /// <summary>
    /// When called on a branch, stores the label branched to.
    /// </summary>
    /// <returns>this.</returns>
    public TranspilerHelper StoreBranchDest()
        => this.GrabBranchDest(out this.label);

    /// <summary>
    /// Gets the labels from a certain instruction. (Primarily used for moving labels).
    /// </summary>
    /// <param name="labels">out labels.</param>
    /// <param name="clear">whether or not to clear the labels.</param>
    /// <returns>this.</returns>
    /// <remarks>DOES NOT CHECK LABELS! YOU SHOULD PROBABLY PUT THEM BACK SOMEWHERE if cleared.</remarks>
    public TranspilerHelper GetLabels(out IList<Label> labels, bool clear = true)
    {
        labels = this.CurrentInstruction.labels.ToList();
        if (clear)
        {
            this.CurrentInstruction.labels.Clear();
        }
        return this;
    }

    /// <summary>
    /// Attaches the labels to the current instruction.
    /// </summary>
    /// <param name="labels">Labels to attach.</param>
    /// <returns>this.</returns>
    public TranspilerHelper AttachLabel(params Label[] labels)
    {
        this.CurrentInstruction.labels.AddRange(labels);
        return this;
    }

    /// <summary>
    /// Attaches the labels to the current instruction.
    /// </summary>
    /// <param name="labels">Labels to attach.</param>
    /// <returns>this.</returns>
    public TranspilerHelper AttachLabels(IEnumerable<Label> labels)
    {
        this.CurrentInstruction.labels.AddRange(labels);
        return this;
    }

    /// <summary>
    /// Defines a new label and attaches it to the current instruction.
    /// </summary>
    /// <param name="label">The label produced.</param>
    /// <returns>this.</returns>
    public TranspilerHelper DefineAndAttachLabel(out Label label)
    {
        label = this.Generator.DefineLabel();
        this.CurrentInstruction.labels.Add(label);
        return this;
    }

    /// <summary>
    /// Finds the first usage of a label in the following section.
    /// </summary>
    /// <param name="label">Label to search for.</param>
    /// <param name="startindex">Index to start searching at (inclusive).</param>
    /// <param name="intendedendindex">Index to end search (exclusive). Leave null to mean "last code".</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentException">StartIndex or EndIndex are invalid.</exception>
    /// <exception cref="InvalidOperationException">No match found.</exception>
    public TranspilerHelper FindFirstLabel(Label label, int startindex = 0, int? intendedendindex = null)
    {
        int endindex = intendedendindex ?? this.Codes.Count;
        if (startindex >= endindex || startindex < 0 || endindex > this.Codes.Count)
        {
            throw new ArgumentException($"Either {nameof(startindex)} {startindex} or {nameof(endindex)} {endindex} are invalid.");
        }
        for (int i = startindex; i < endindex; i++)
        {
            if (this.Codes[i].labels.Contains(label))
            {
                this.Pointer = i;
                return this;
            }
        }
        throw new InvalidOperationException($"label {label} could not be found between {startindex} and {endindex}");
    }

    /// <summary>
    /// Moves pointer forward to the label.
    /// </summary>
    /// <param name="label">Label to search for.</param>
    /// <returns>this.</returns>
    /// <exception cref="IndexOutOfRangeException">No match found.</exception>
    public TranspilerHelper AdvanceToLabel(Label label)
        => this.FindFirstLabel(label, this.Pointer + 1, this.Codes.Count);

    /// <summary>
    /// Advances to the stored label.
    /// </summary>
    /// <returns>this.</returns>
    /// <exception cref="InvalidOperationException">No label stored.</exception>
    /// <exception cref="IndexOutOfRangeException">No match found.</exception>
    public TranspilerHelper AdvanceToStoredLabel()
    {
        if (this.label is null)
        {
            throw new InvalidOperationException("Attempted to advance to label, but there is not one stored!");
        }
        return this.AdvanceToLabel(this.label.Value);
    }

    /// <summary>
    /// Finds the last usage of a label in the following section.
    /// </summary>
    /// <param name="label">Label to search for.</param>
    /// <param name="startindex">Index to start searching at (inclusive).</param>
    /// <param name="intendedendindex">Index to end search (exclusive). Leave null to mean "last code".</param>
    /// <returns>this.</returns>
    /// <exception cref="ArgumentException">StartIndex or EndIndex are invalid.</exception>
    /// <exception cref="IndexOutOfRangeException">No match found.</exception>
    public TranspilerHelper FindLastLabel(Label label, int startindex = 0, int? intendedendindex = null)
    {
        int endindex = intendedendindex ?? this.Codes.Count;
        if (startindex >= endindex || startindex < 0 || endindex > this.Codes.Count)
        {
            throw new ArgumentException($"Either {nameof(startindex)} {startindex} or {nameof(endindex)} {endindex} are invalid.");
        }
        for (int i = endindex - 1; i >= startindex; i--)
        {
            if (this.Codes[i].labels.Contains(label))
            {
                this.Pointer = i;
                return this;
            }
        }
        throw new Exception($"replace with custom exception - label {label} could not be found between {startindex} and {endindex}");
    }

    /// <summary>
    /// Moves pointer backwards to the label.
    /// </summary>
    /// <param name="label">Label to search for.</param>
    /// <returns>this.</returns>
    /// <exception cref="IndexOutOfRangeException">No match found.</exception>
    public TranspilerHelper RetreatToLabel(Label label)
        => this.FindLastLabel(label, 0, this.Pointer);

    /// <summary>
    /// Retreat to the stored label.
    /// </summary>
    /// <returns>this.</returns>
    /// <exception cref="InvalidOperationException">No label stored.</exception>
    /// <exception cref="IndexOutOfRangeException">No match found.</exception>
    public TranspilerHelper RetreatToStoredLabel()
    {
        if (this.label is null)
        {
            throw new InvalidOperationException("Attempted to advance to label, but there is not one stored!");
        }
        return this.RetreatToLabel(this.label.Value);
    }

    #endregion


    public static CodeInstruction ToLdLoc(CodeInstruction instruction)
    {
        OpCode code = instruction.opcode;
        if (code == OpCodes.Ldloc_0 || code == OpCodes.Stloc_0)
        {
            return new CodeInstruction(OpCodes.Ldloc_0);
        }
        else if (code == OpCodes.Ldloc_1 || code == OpCodes.Stloc_1)
        {
            return new CodeInstruction(OpCodes.Ldloc_1);
        }
        else if (code == OpCodes.Ldloc_2 || code == OpCodes.Stloc_2)
        {
            return new CodeInstruction(OpCodes.Ldloc_2);
        }
        else if (code == OpCodes.Ldloc_3 || code == OpCodes.Stloc_3)
        {
            return new CodeInstruction(OpCodes.Ldloc_3);
        }
        else if (code == OpCodes.Ldloc || code == OpCodes.Stloc)
        {
            return new CodeInstruction(OpCodes.Ldloc, instruction.operand);
        }
        else if (code == OpCodes.Ldloc_S || code == OpCodes.Stloc_S)
        {
            return new CodeInstruction(OpCodes.Ldloc_S, instruction.operand);
        }
        else if (code == OpCodes.Ldloca || code == OpCodes.Ldloca_S)
        {
            return instruction.Clone();
        }
        throw new InvalidOperationException($"Could not make ldloc from {instruction}");
    }

    public static CodeInstruction ToStLoc(CodeInstruction instruction)
    {
        OpCode code = instruction.opcode;
        if (code == OpCodes.Ldloc_0 || code == OpCodes.Stloc_0)
        {
            return new CodeInstruction(OpCodes.Stloc_0);
        }
        else if (code == OpCodes.Ldloc_1 || code == OpCodes.Stloc_1)
        {
            return new CodeInstruction(OpCodes.Stloc_1);
        }
        else if (code == OpCodes.Ldloc_2 || code == OpCodes.Stloc_2)
        {
            return new CodeInstruction(OpCodes.Stloc_2);
        }
        else if (code == OpCodes.Ldloc_3 || code == OpCodes.Stloc_3)
        {
            return new CodeInstruction(OpCodes.Stloc_3);
        }
        else if (code == OpCodes.Ldloc || code == OpCodes.Stloc)
        {
            return new CodeInstruction(OpCodes.Stloc, instruction.operand);
        }
        else if (code == OpCodes.Ldloc_S || code == OpCodes.Stloc_S)
        {
            return new CodeInstruction(OpCodes.Stloc_S, instruction.operand);
        }
        throw new InvalidOperationException($"Could not make stloc from {instruction}");
    }

    /// <summary>
    /// Handles matching wrappers against instructions. Has special handling for the first three locals.
    /// </summary>
    /// <param name="wrapper">The CodeInstruction.</param>
    /// <param name="instruction">The instruction to match against.</param>
    /// <returns>True if matches, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool IsMatch(CodeInstruction instruction1, CodeInstruction instruction2)
    {
        if (instruction1 is not null
            && ((instruction1.operand is null && instruction1.opcode == instruction2.opcode)
                || instruction1.Is(instruction2.opcode, instruction2.operand))
                || (instruction1.opcode == OpCodes.Ldloc && instruction2.IsLdloc())
                || (instruction1.opcode == OpCodes.Stloc && instruction2.IsStloc()))
        {
            return true;
        }
        return false;
    }
}