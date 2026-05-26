using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.JSInterop;
namespace PhysicsSimApp.Components;

public class Object
{
    public float magneticFieldStrength;
    public bool isMagnetic;
    public float charge;
    public float resistance;
    public float conductivity;
    public float mass;
    public bool isStatic;
    public bool isBattery;
    public float voltage;

    public Vector2 pos;
    public Vector2 center;
    public Vector2 velocity;
    public float current;
    public Vector2 currentPosDir;

    public float length;
    public float height;
    public string color;

    public float friction = 1f;
    public float collideScale = 1f;
    public void ApplyVelocity(float dt)
    {
        pos += velocity*dt;
        center = new Vector2(pos.X + length * 0.5f, pos.Y + height * 0.5f);
        if (pos.X < 0 || pos.X > 1000)
        {
            velocity = new Vector2(-velocity.X, velocity.Y);
        }
        if (pos.Y < 0 || pos.Y > 500)
        {
            velocity = new Vector2(velocity.X, -velocity.Y);
        }
        velocity = new Vector2(velocity.X * friction, velocity.Y * friction);
    }

    public void BuildObject(float magneticFieldStrength, bool isMagnetic, float charge, float resistance, float conductivity, float mass, bool isStatic, bool isBattery, float voltage, Vector2 pos, Vector2 velocity, float length, float height, string color, float friction, float collideScale)
    {
        this.magneticFieldStrength = magneticFieldStrength;
        this.isMagnetic = isMagnetic;
        this.charge = charge;
        this.resistance = resistance;
        this.conductivity = conductivity;
        this.mass = mass;
        this.isStatic = isStatic;
        this.isBattery = isBattery;
        this.voltage = voltage;
        this.pos = pos;
        this.velocity = velocity;
        this.length = length;
        this.height = height;
        this.color = color;
        this.friction = 1-friction;
        this.collideScale = collideScale;
        this.center = new Vector2(pos.X + length * 0.5f, pos.Y + height * 0.5f);
    }

    public async Task BuildObjectAsync(IJSRuntime JS)
    {
        await JS.InvokeVoidAsync("sim.buildCube", pos.X, pos.Y, length, height, color);
    }
}
public class Circuit()
{
    public List<PhysicsSimApp.Components.Object> objects = new List<PhysicsSimApp.Components.Object>();
    public Object battery;
    public float totalVoltage = 0;
    public float totalCurrent = 0;
    public float EquivalentResistance;
    public float current;
    public Manager manager;

    public void CreateCircuit(List<PhysicsSimApp.Components.Object> objects, Manager manager)
    {
        objects = objects.Distinct().ToList();
        this.objects = objects.Distinct().ToList();
        this.battery = objects.FirstOrDefault(o => o.isBattery);
        this.manager = manager; 
        this.totalVoltage = battery.voltage;
        this.GetEquivalentResistance();
    }
    public bool isComplete()
    {
       return this.EquivalentResistance > 0;
    }
    public void GetEquivalentResistance()
    {
        var closeGuys = GetNeighbors();
        var parallelLoops = FindBatteryCycles(closeGuys);

        if (parallelLoops.Count == 0)
        {
            Console.WriteLine("incomplete circuit");
            EquivalentResistance = -1;
            return;
        }

        var branchResistances = parallelLoops.Select(CalcLoopResistance).ToList();
        float parallelSum = 0f;
        foreach (var branchResistance in branchResistances)
        {
            if (branchResistance <= 0f) continue;
            parallelSum += 1f / branchResistance;
        }

        if (parallelSum <= 0f)
        {
            Console.WriteLine("incomplete circuit");
            EquivalentResistance = -1;
            return;
        }

        float networkResistance = 1 / parallelSum;
        EquivalentResistance = battery.resistance + networkResistance;
        Console.WriteLine($"equivalent circuit resistance {EquivalentResistance} with {parallelLoops.Count} parallel loops");
        totalCurrent = battery.voltage/EquivalentResistance;
        GetCurrentEverywhere();
    }
 public void GetCurrentEverywhere()
    {
        if (battery == null)
            return;

        foreach (var obj in objects)
        {
            obj.current = 0;
            obj.currentPosDir = Vector2.Zero;
            obj.voltage = 0;
        }

        var visited = new HashSet<Object>();
        PropagateCurrent(battery, null, totalCurrent, totalVoltage, visited);
    }

    private void PropagateCurrent(Object currentObj,Object parent,float incomingCurrent,float incomingVoltage,HashSet<Object> visited)
    {
        if (visited.Contains(currentObj))
            return;

        visited.Add(currentObj);
        Console.WriteLine($"inbound current: {incomingCurrent}, inbound voltage: {incomingVoltage}");
        currentObj.current = incomingCurrent;
        currentObj.voltage += incomingVoltage;

        if (parent != null)
        {
            var dir = currentObj.center - parent.center;
            if (dir.LengthSquared() > 0)
            {
                currentObj.currentPosDir = Vector2.Normalize(dir);
            }
        }

        float voltageDrop = incomingCurrent * currentObj.resistance;
        float remainingVoltage = incomingVoltage - voltageDrop;

        if (remainingVoltage < 0) remainingVoltage = 0;

        var nextNeighbors = manager.GetCollisions(currentObj).Where(n => n != parent).ToList();

        if (nextNeighbors.Count == 0)
            return;
        if (nextNeighbors.Count == 1)
        {
            PropagateCurrent(nextNeighbors[0], currentObj, incomingCurrent, remainingVoltage, visited);
            return;
        }

        float totalConductance = 0f;
        foreach (var next in nextNeighbors)
        {
            float r = Math.Max(next.resistance, 0.0001f);

            totalConductance += 1f / r;
        }
        foreach (var next in nextNeighbors)
        {
            float r = Math.Max(next.resistance, 0.0001f);

            float conductance = 1 / r;

            float branchCurrent =incomingCurrent *(conductance / totalConductance);
            PropagateCurrent(next, currentObj, branchCurrent, remainingVoltage, visited);
        }
    }
    private Dictionary<Object, List<Object>> GetNeighbors()
    {
        var closeGuys = new Dictionary<Object, List<Object>>();
        foreach (var obj in objects)
        {
            closeGuys[obj] = new List<Object>();
        }

        foreach (var obj in objects)
        {
            var neighbors = manager.GetCollisions(obj);
            foreach (var neighbor in neighbors)
            {
                if (!objects.Contains(neighbor))
                    continue;

                if (!closeGuys[obj].Contains(neighbor))
                    closeGuys[obj].Add(neighbor);

                if (!closeGuys[neighbor].Contains(obj))
                    closeGuys[neighbor].Add(obj);
            }
        }

        return closeGuys;
    }

    private List<List<Object>> FindBatteryCycles(Dictionary<Object, List<Object>> closeGuys)
    {
        if (battery == null || !closeGuys.ContainsKey(battery))
            return new List<List<Object>>();

        var cycles = new List<List<Object>>();
        var basedCycleHash = new HashSet<string>();
        var visited = new HashSet<Object> { battery };
        foreach (var neighbor in closeGuys[battery])
        {
            var path = new List<Object> { battery, neighbor };
            FindCyclesRecursive(neighbor, path, visited, closeGuys, basedCycleHash, cycles);
        }

        return cycles;
    }

    private void FindCyclesRecursive(Object current, List<Object> path, HashSet<Object> visited, Dictionary<Object, List<Object>> closeGuys, HashSet<string> basedCycleHash, List<List<Object>> cycles)
    {
        if (current == battery)
        {
            if (path.Count > 2)
            {
                var key = GetParallelLoopKey(path);
                if (!basedCycleHash.Contains(key))
                {
                    basedCycleHash.Add(key);
                    cycles.Add(new List<Object>(path));
                }
            }
            return;
        }
        if (visited.Contains(current))
            return;

        visited.Add(current);
        foreach (var next in closeGuys[current])
        {
            if (path.Count > 1 && next == path[path.Count - 2])
                continue;

            path.Add(next);
            FindCyclesRecursive(next, path, visited, closeGuys, basedCycleHash, cycles);
            path.RemoveAt(path.Count - 1);
        }
        visited.Remove(current);
    }
    private float CalcLoopResistance(List<Object> cycle)
    {
        return cycle.Skip(1).Take(cycle.Count - 2).Sum(o => o.resistance);
    }
    private string GetParallelLoopKey(List<Object> cycle)
    {
        var ids = cycle.Take(cycle.Count - 1).Select(o => o.GetHashCode()).ToList();
        var og = string.Join(",", ids);
        var reversed = ids.AsEnumerable().Reverse().ToList();
        og = string.CompareOrdinal(og, string.Join(",", reversed)) <= 0 ? og : string.Join(",", reversed);

        int count = ids.Count;
        for (int i = 1; i < count; i++)
        {
            var rotated = ids.Skip(i).Concat(ids.Take(i)).ToList();
            var candidate = string.Join(",", rotated);
            if (string.CompareOrdinal(candidate, og) < 0)
                og = candidate;

            var rotatedReversed = rotated.AsEnumerable().Reverse().ToList();
            candidate = string.Join(",", rotatedReversed);
            if (string.CompareOrdinal(candidate, og) < 0)
                og = candidate;
        }
        return og;
    }
}
public class Manager
{
    public const float K_COULOMB = 8987550000;
    public float distScale = 0.01f;
    public List<Object> objects = new List<Object>();
    public List<Circuit> circuits = new List<Circuit>();
    public float deltaTime = 0.1f;

    public void ApplyForce(Vector2 force, Object o)
    {
        o.velocity += (force / o.mass) * deltaTime;
        Console.WriteLine($"Force of {force} applied to object, new velocity is {o.velocity}");
    }

public void ApplyElasticCollision(Object o, Object oi)
{
    Vector2 posDiff = o.center - oi.center;
    float dist2 = posDiff.LengthSquared();
    if (dist2 == 0f) return;

    Vector2 v1 = o.velocity;
    Vector2 v2 = oi.velocity;

    float m1 = o.mass;
    float m2 = oi.mass;

    float dot1 = Vector2.Dot(v1 - v2, posDiff);

    Vector2 impulse = (dot1 / dist2) * posDiff;

    o.velocity  = v1 - (2 * m2 / (m1 + m2)) * impulse;
    oi.velocity = v2 + (2 * m1 / (m1 + m2)) * impulse;
}
public List<Object> GetCollisions(Object o)
{
    List<Object> touchytouchy = new List<Object>();
    
        float oMinX = o.center.X - o.length / 2;
        float oMaxX = o.center.X + o.length / 2;
        float oMinY = o.center.Y - o.height / 2;
        float oMaxY = o.center.Y + o.height / 2;

        for (int i = 0; i < objects.Count; i++)
        {
            Object oi = objects[i];
            if (oi == o) continue;

            float oiMinX = oi.center.X - oi.length / 2;
            float oiMaxX = oi.center.X + oi.length / 2;
            float oiMinY = oi.center.Y - oi.height / 2;
            float oiMaxY = oi.center.Y + oi.height / 2;
            bool overlapX = oMaxX > oiMinX && oMinX < oiMaxX;
            bool overlapY = oMaxY > oiMinY && oMinY < oiMaxY;
        if (overlapX && overlapY)
        {
            touchytouchy.Add(oi);
        }
    }
    return touchytouchy;
}
public bool CheckCollision(Object o)
{
        float oMinX = (o.center.X - (o.length  * o.collideScale/ 2));
        float oMaxX = (o.center.X + (o.length * o.collideScale / 2));
        float oMinY = (o.center.Y - (o.height * o.collideScale / 2));
        float oMaxY = (o.center.Y + (o.height  * o.collideScale/ 2) );

        for (int i = 0; i < objects.Count; i++)
        {
            Object oi = objects[i];
            if (oi == o) continue;

            float oiMinX = oi.center.X - oi.length * oi.collideScale / 2;
            float oiMaxX = oi.center.X + oi.length * oi.collideScale / 2;
            float oiMinY = oi.center.Y - oi.height * oi.collideScale / 2;
            float oiMaxY = oi.center.Y + oi.height * oi.collideScale / 2;
            bool overlapX = oMaxX > oiMinX && oMinX < oiMaxX;
            bool overlapY = oMaxY > oiMinY && oMinY < oiMaxY;

        float overlapXX = Math.Min(oMaxX, oiMaxX) - Math.Max(oMinX, oiMinX);
        float overlapYY = Math.Min(oMaxY, oiMaxY) - Math.Max(oMinY, oiMinY);

        if (overlapX && overlapY)
        {
            if (oi.isStatic)
            {
                if (overlapXX < overlapYY)
                {
                    o.velocity = new Vector2(-o.velocity.X, o.velocity.Y);
                }
                else
                {
                    o.velocity = new Vector2(o.velocity.X, -o.velocity.Y);
                }
                o.center = new Vector2(o.pos.X + o.length * 0.5f, o.pos.Y + o.height * 0.5f);
            }
            else
            {
                ApplyElasticCollision(o, oi);
            }

            return true;
        }
    }

    return false;
}

public void CheckMagneticForces(Object o)
{
    if (!o.isStatic && o.isMagnetic)
    {
        foreach (var m in objects)
        {
            if (m.magneticFieldStrength == 0) continue;
            if (m == o) continue;
            
            Vector2 diff = m.center - o.center;
            if (diff.Length() > 500) continue;
            float distSq = diff.LengthSquared();
            if (distSq < 0.0001f) continue;

            float dist = MathF.Sqrt(distSq);

            Vector2 dir = diff / dist;
            dist = dist * distScale;
            float forceMagnitude = MathF.Abs(m.magneticFieldStrength * o.magneticFieldStrength / (dist * dist * dist));
            if (forceMagnitude <= 0.1)
                continue;
            else if (forceMagnitude > MathF.Abs(m.magneticFieldStrength*o.magneticFieldStrength))
            {
                forceMagnitude = MathF.Abs(m.magneticFieldStrength*o.magneticFieldStrength);
            }
            Vector2 force = dir * forceMagnitude * (m.magneticFieldStrength * o.magneticFieldStrength < 0 ? 1 : -1);

            ApplyForce(force, o);
        }
    }
}

    public void CheckElectricForces(Object o)
    {
    if (!o.isStatic && o.charge != 0)
    {
        foreach (var m in objects)
        {
            if (m.charge == 0) continue;
            if (m == o) continue;

            Vector2 diff = m.center - o.center;
            if (diff.Length() > 500) continue;
            float distSq = diff.LengthSquared();
            if (distSq < 0.0001f) continue;

            float dist = MathF.Sqrt(distSq);

            Vector2 dir = diff / dist;
            dist = dist * distScale;
            float forceMagnitude = MathF.Abs(m.charge * o.charge * K_COULOMB / (dist * dist));
            if (forceMagnitude <= 0.1)
                continue;
            else if (forceMagnitude > MathF.Abs(m.charge*o.charge) * K_COULOMB)
            {
                forceMagnitude = MathF.Abs(m.charge*o.charge * K_COULOMB);
            }
            Vector2 force = dir * forceMagnitude * (m.charge * o.charge < 0 ? 1 : -1);
            ApplyForce(force, o);
        }
    }
    }
public void CheckLorentzForces(Object o)
{
    if (o.isStatic)
        return;

    if (MathF.Abs(o.charge) <= 0.001f
        && MathF.Abs(o.current) <= 0.001f)
    {
        return;
    }

    Vector2 totalForce = Vector2.Zero;

    foreach (var m in objects)
    {
        if (m == o)
            continue;
        Vector2 diff = o.center - m.center;
        float distSq = MathF.Max(diff.LengthSquared(), 100f);
        float dist = MathF.Sqrt(distSq);
        if (dist > 500)
            continue;
        distSq *= distScale * distScale;
        dist *= distScale;
        float fieldStrength = 0f;
        fieldStrength += m.magneticFieldStrength / distSq;
        fieldStrength += MathF.Abs(m.current) / dist;

        Vector2 fieldDir;
        if (m.currentPosDir.LengthSquared() > 0.001f)
        {
            fieldDir =new Vector2(m.currentPosDir.Y,-m.currentPosDir.X);
        }
        else
        {
            fieldDir = Vector2.Normalize(diff);
        }

        Vector2 movingDir = Vector2.Zero;

        if (o.velocity.LengthSquared() > 0.001f)
        {
            movingDir = Vector2.Normalize(o.velocity);
        }
        else if (o.currentPosDir.LengthSquared() > 0.001f)
        {
            movingDir = o.currentPosDir;
        }

        if (movingDir.LengthSquared() <= 0.001f)
            continue;

        Vector2 perp =new Vector2(-fieldDir.Y,fieldDir.X);

        float strength = fieldStrength * (MathF.Abs(o.charge)+ MathF.Abs(o.current) );

        Vector2 force = perp * strength;

        if (o.charge < 0) force *= -1;
        totalForce += force;
    }

    ApplyForce(totalForce, o);
}
    public void AddCircuit(List<Object> os)
    {
bool exists = circuits.Any(circuit => circuit.objects.SequenceEqual(os));
if (!exists)
{
    Circuit newCircuit = new Circuit();
    newCircuit.CreateCircuit(os, this);
    circuits.Add(newCircuit);
    if (circuits.Last().isComplete())
    {
        foreach (Object o in os)
        {
            if (o == circuits.Last().battery)
            {
            o.color = "#fd5219";
            }
            else 
            {
                o.color = "#e7dd1a";
            }
        }
    }
    Console.WriteLine($"circuit added with {os.Count} objects");
}

     }
}
