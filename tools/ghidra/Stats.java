// Prints coverage stats: functions, instruction bytes per executable block, and entropy of .text.
// @category SimsRE

import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import ghidra.program.model.mem.MemoryBlock;

public class Stats extends GhidraScript {
    @Override
    protected void run() throws Exception {
        println("functions: " + currentProgram.getFunctionManager().getFunctionCount());
        for (MemoryBlock b : currentProgram.getMemory().getBlocks()) {
            if (!b.isExecute()) continue;
            long ins = 0;
            InstructionIterator it = currentProgram.getListing().getInstructions(
                new ghidra.program.model.address.AddressSet(b.getStart(), b.getEnd()), true);
            while (it.hasNext()) ins += it.next().getLength();
            byte[] buf = new byte[(int) b.getSize()];
            b.getBytes(b.getStart(), buf);
            long[] freq = new long[256];
            for (byte x : buf) freq[x & 0xff]++;
            double h = 0;
            for (long f : freq) if (f > 0) { double p = (double) f / buf.length; h -= p * Math.log(p) / Math.log(2); }
            println(String.format("%s size=%d instrBytes=%d (%.1f%%) entropy=%.2f", b.getName(), b.getSize(),
                ins, 100.0 * ins / b.getSize(), h));
        }
    }
}
