// For each given address that has no containing function: walks back to the start of the code run
// (stops at int3/nop padding or the end of a previous function), disassembles and creates a function.
// Then sweeps the whole of .text for orphaned code runs that start after padding and creates those too.
// Args: <addr> [addr...]
// @category SimsRE

import ghidra.app.cmd.disassemble.DisassembleCommand;
import ghidra.app.cmd.function.CreateFunctionCmd;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;

public class MakeFunctions extends GhidraScript {
    private int created = 0;

    @Override
    protected void run() throws Exception {
        for (String a : getScriptArgs()) {
            fix(toAddr(a));
        }
        MemoryBlock text = currentProgram.getMemory().getBlock(".text");
        sweep(text.getStart(), text.getEnd());
        println("created functions: " + created);
    }

    private void fix(Address a) throws Exception {
        if (getFunctionContaining(a) != null) return;
        Address start = findRunStart(a);
        create(start);
    }

    private Address findRunStart(Address a) throws Exception {
        Memory mem = currentProgram.getMemory();
        Address p = a;
        while (true) {
            Address prev = p.subtract(1);
            int b = mem.getByte(prev) & 0xff;
            if (b == 0xcc || getFunctionContaining(prev) != null) return p;
            p = prev;
        }
    }

    // creates functions at every address in .text that follows int3 padding, is 16-byte aligned,
    // and is not already inside a function
    private void sweep(Address start, Address end) throws Exception {
        Memory mem = currentProgram.getMemory();
        Address a = start.add(16);
        while (a.compareTo(end) < 0 && !monitor.isCancelled()) {
            if ((a.getOffset() & 0xf) == 0 && getFunctionContaining(a) == null
                    && (mem.getByte(a.subtract(1)) & 0xff) == 0xcc
                    && (mem.getByte(a) & 0xff) != 0xcc) {
                create(a);
            }
            a = a.add(16);
        }
    }

    private void create(Address a) {
        if (getInstructionAt(a) == null) {
            new DisassembleCommand(a, null, true).applyTo(currentProgram, monitor);
        }
        Instruction i = getInstructionAt(a);
        if (i == null) return;
        if (new CreateFunctionCmd(a).applyTo(currentProgram, monitor)) created++;
    }
}
