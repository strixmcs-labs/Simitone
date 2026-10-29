// Lists functions by the number of distinct switch cases Ghidra recovered (caseD_* labels).
// Args: <out dir> [min cases] [max cases]   Output: <out>/switches.txt
// @category SimsRE

import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolIterator;

import java.io.File;
import java.io.PrintWriter;
import java.util.*;

public class ListSwitches extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        int min = args.length > 1 ? Integer.parseInt(args[1]) : 30;
        int max = args.length > 2 ? Integer.parseInt(args[2]) : 90;
        Map<Function, Set<Long>> cases = new HashMap<>();
        SymbolIterator it = currentProgram.getSymbolTable().getAllSymbols(true);
        while (it.hasNext() && !monitor.isCancelled()) {
            Symbol s = it.next();
            String n = s.getName();
            if (!n.startsWith("caseD_")) continue;
            Function f = getFunctionContaining(s.getAddress());
            if (f == null) continue;
            try {
                cases.computeIfAbsent(f, k -> new TreeSet<>()).add(Long.parseLong(n.substring(6), 16));
            } catch (NumberFormatException e) {
                // ignore odd labels
            }
        }
        try (PrintWriter w = new PrintWriter(new File(args[0], "switches.txt"))) {
            cases.entrySet().stream()
                .filter(e -> e.getValue().size() >= min && e.getValue().size() <= max)
                .sorted(Comparator.comparing(e -> e.getKey().getEntryPoint()))
                .forEach(e -> {
                    TreeSet<Long> v = (TreeSet<Long>) e.getValue();
                    w.printf("%s %-28s cases=%d range=%d..%d size=%d%n", e.getKey().getEntryPoint(),
                        e.getKey().getName(), v.size(), v.first(), v.last(),
                        e.getKey().getBody().getNumAddresses());
                });
        }
        println("functions with switches: " + cases.size());
    }
}
