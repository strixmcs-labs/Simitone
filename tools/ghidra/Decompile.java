// Decompiles the functions at the given addresses to <out>/<addr>.c
// Args: <out dir> <addr> [addr...]
// @category SimsRE

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;

import java.io.File;
import java.io.PrintWriter;

public class Decompile extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        File dir = new File(args[0]);
        dir.mkdirs();
        DecompInterface d = new DecompInterface();
        d.openProgram(currentProgram);
        for (int i = 1; i < args.length; i++) {
            Function f = getFunctionContaining(toAddr(args[i]));
            if (f == null) {
                println("no function at " + args[i]);
                continue;
            }
            DecompileResults r = d.decompileFunction(f, 120, monitor);
            try (PrintWriter w = new PrintWriter(new File(dir, f.getEntryPoint() + ".c"))) {
                w.println("// " + f.getName() + " @ " + f.getEntryPoint());
                w.println(r.decompileCompleted() ? r.getDecompiledFunction().getC() : r.getErrorMessage());
            }
        }
        d.dispose();
    }
}
