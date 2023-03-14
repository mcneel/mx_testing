import rhinoscriptsyntax as rs

ms = rs.GetObjects(filter=rs.filter.mesh)
if ms:
    
    text = ["MEASURED SPLITS",""]
    areas = []
    for m in ms:
        a = rs.Area(m)
        areas.append(a)
        
    areas = sorted(areas)
    
    for a in areas:
        text.append(repr(a))
    
    
text = "\n".join(text)

#print("OLD NOTES: ")
print(rs.Notes(text))