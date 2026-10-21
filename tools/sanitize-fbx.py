"""Sanitize FBX path metadata without changing geometry or Unity GUIDs."""
import argparse, pathlib, re, struct

def sanitize_fbx(data):
    assert data.startswith(b'Kaydara FBX Binary  \x00\x1a\x00')
    version = struct.unpack_from('<I', data, 23)[0]
    fmt = '<QQQB' if version >= 7500 else '<IIIB'
    hs = struct.calcsize(fmt)
    edits = []
    def properties(raw, count):
        pos = 0; result = bytearray()
        for _ in range(count):
            start = pos; typ = chr(raw[pos]); pos += 1
            if typ in 'YCFDIL': pos += {'Y':2,'C':1,'F':4,'D':8,'I':4,'L':8}[typ]
            elif typ in 'SR':
                n = struct.unpack_from('<I',raw,pos)[0]; pos += 4
                value = raw[pos:pos+n]; pos += n
                if typ == 'S' and re.search(rb'(?i)[a-z]:[\\/]',value):
                    # Only path string properties change; preserve every other byte.
                    clean = re.split(rb'[\\/]',value)[-1]
                    assert b'\0' not in clean and clean
                    edits.append((value.decode(errors='replace'),clean.decode(errors='replace')))
                    result.extend(b'S'+struct.pack('<I',len(clean))+clean)
                    continue
            elif typ in 'fdlibc':
                count_,encoding,n = struct.unpack_from('<III',raw,pos); pos += 12+n
            else: raise ValueError('Unknown FBX property type '+typ)
            result.extend(raw[start:pos])
        assert pos == len(raw)
        return bytes(result)
    def parse(pos):
        end,n,pl,nl = struct.unpack_from(fmt,data,pos)
        if end == 0:
            assert data[pos:pos+hs] == bytes(hs)
            return None,pos+hs
        pos += hs; name=data[pos:pos+nl];pos+=nl
        props=properties(data[pos:pos+pl],n);pos+=pl
        children=[];terminal=False
        while pos < end:
            child,pos=parse(pos)
            if child is None:
                terminal=True; assert pos==end;break
            children.append(child)
        assert pos==end
        return (name,n,props,children,terminal),pos
    nodes=[];pos=27
    while True:
        node,pos=parse(pos)
        if node is None:break
        nodes.append(node)
    footer=data[pos:]
    def write(node,offset):
        name,n,props,children,terminal=node
        out=bytearray(bytes(hs)+name+props)
        for child in children:out.extend(write(child,offset+len(out)))
        if terminal:out.extend(bytes(hs))
        out[:hs]=struct.pack(fmt,offset+len(out),n,len(props),len(name))
        return out
    out=bytearray(data[:27])
    for node in nodes:out.extend(write(node,len(out)))
    out.extend(bytes(hs))
    # Keep the footer's original alignment after changing string lengths.
    oldpad = (-(pos+16)) % 16
    if len(footer)>=16+oldpad and footer[16:16+oldpad]==bytes(oldpad):
        newpad=(-(len(out)+16))%16
        footer=footer[:16]+bytes(newpad)+footer[16+oldpad:]
    out.extend(footer)
    return bytes(out),edits


def main():
    cli=argparse.ArgumentParser(description=__doc__)
    mode=cli.add_mutually_exclusive_group(required=True)
    mode.add_argument('--check',action='store_true')
    mode.add_argument('--write',action='store_true')
    cli.add_argument('paths',nargs='+',type=pathlib.Path)
    args=cli.parse_args();dirty=0;count=0
    for path in args.paths:
        files=sorted(path.rglob('*.fbx')) if path.is_dir() else [path]
        if not files:cli.error('No FBX files found: '+str(path))
        for file in files:
            original=file.read_bytes();clean,edits=sanitize_fbx(original);count+=1
            if not edits:continue
            dirty+=1
            if args.write:
                backup=file.with_name(file.name+'.private-backup')
                if not backup.exists():backup.write_bytes(original)
                file.write_bytes(clean)
            print(str(file)+': '+str(len(edits))+' absolute path strings '+('sanitized' if args.write else 'found'))
    print(str(count)+' FBX files checked; '+str(dirty)+' '+('sanitized.' if args.write else 'need sanitizing.'))
    return 1 if dirty and args.check else 0

if __name__=='__main__':raise SystemExit(main())
