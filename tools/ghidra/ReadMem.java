// Prints memory as little-endian values. Args: <addr>:<count>:<i32|i16|f32> [...]
// @category SimsRE

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;

public class ReadMem extends GhidraScript {
    @Override
    protected void run() throws Exception {
        for (String spec : getScriptArgs()) {
            String[] p = spec.split(":");
            Address a = toAddr(p[0]);
            int n = Integer.parseInt(p[1]);
            StringBuilder sb = new StringBuilder(spec + " =");
            for (int i = 0; i < n; i++) {
                switch (p[2]) {
                    case "i16": sb.append(' ').append(getShort(a.add(i * 2L))); break;
                    case "f32": sb.append(' ').append(Float.intBitsToFloat(getInt(a.add(i * 4L)))); break;
                    default: sb.append(' ').append(getInt(a.add(i * 4L)));
                }
            }
            println(sb.toString());
        }
    }
}
