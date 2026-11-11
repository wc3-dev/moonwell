-- Moonwell's Lua for a development build only, linked after moonwell.lua (decisions 262 and 263): ended objects kept
-- weakly, the game's handles counted as natives make and destroy them, the line that made a timer, the profiler's
-- timing of the map's own reactions, and the loop guard. A release build links none of it.

-- Ended objects, kept weakly with the time each ended, and canaries, so that only an object that outlived a full
-- collection counts: the collector runs on what is made, not on time (game facts, 11-10).
local heldObjects, heldCanaries, canaryTimes, collectedAt, heldSummary

function MoonwellHeldReset()
  heldObjects = setmetatable({}, { __mode = "k" })
  heldCanaries = setmetatable({}, { __mode = "k" })
  canaryTimes = {}
  collectedAt = -math.huge
  heldSummary = ""
end
MoonwellHeldReset()

function MoonwellHeldAdd(obj, now)
  heldObjects[obj] = now
end

function MoonwellHeldCanary(now)
  heldCanaries[{}] = now
  canaryTimes[#canaryTimes + 1] = now
end

-- "Footman 120, Rage 30": the most first, then by name, at most five, as Leaks.Kinds writes them.
local function kinds(counts)
  local names = {}
  for name in pairs(counts) do names[#names + 1] = name end
  table.sort(names, function (a, b)
    if counts[a] ~= counts[b] then return counts[a] > counts[b] end
    return a < b
  end)
  local parts = {}
  for i = 1, math.min(5, #names) do parts[i] = names[i] .. " " .. counts[names[i]] end
  local text = table.concat(parts, ", ")
  if #names > 5 then text = text .. " and " .. (#names - 5) .. " other kinds" end
  return text
end

function MoonwellHeldCount(now, age)
  local present = {}
  for _, at in pairs(heldCanaries) do present[at] = true end
  local kept = {}
  for i = 1, #canaryTimes do
    local at = canaryTimes[i]
    if present[at] then kept[#kept + 1] = at elseif at > collectedAt then collectedAt = at end
  end
  canaryTimes = kept
  local counts, total = {}, 0
  for obj, ended in pairs(heldObjects) do
    if ended < collectedAt and now - ended >= age then
      total = total + 1
      local name = System.typeof(getmetatable(obj)):getName()
      counts[name] = (counts[name] or 0) + 1
    end
  end
  heldSummary = kinds(counts)
  return total
end

function MoonwellHeldSummary()
  return heldSummary
end

-- The game's handles, counted as made and destroyed: each native that makes a handle of a kind that leaks is wrapped
-- once, at the start, and so is the one that destroys it. Natives are called as globals, so every call is seen, the
-- map's and Moonwell's. A text tag is left out, as the game destroys one with a lifespan by itself.
local handleKinds = {
  { "location", { "Location", "GetUnitLoc", "GetRectCenter", "GetSpellTargetLoc", "GetOrderPointLoc", "GetCameraTargetPositionLoc", "GetCameraEyePositionLoc", "GetStartLocationLoc", "CameraSetupGetDestPositionLoc", "GetUnitRallyPoint", "BlzGetTriggerPlayerMousePosition" }, { "RemoveLocation" } },
  { "group", { "CreateGroup" }, { "DestroyGroup" } },
  { "force", { "CreateForce" }, { "DestroyForce" } },
  { "rect", { "Rect", "RectFromLoc", "GetWorldBounds" }, { "RemoveRect" } },
  { "region", { "CreateRegion" }, { "RemoveRegion" } },
  { "effect", { "AddSpecialEffect", "AddSpecialEffectLoc", "AddSpecialEffectTarget", "AddSpellEffect", "AddSpellEffectLoc", "AddSpellEffectById", "AddSpellEffectByIdLoc", "AddSpellEffectTarget", "AddSpellEffectTargetById" }, { "DestroyEffect" } },
  { "lightning", { "AddLightning", "AddLightningEx" }, { "DestroyLightning" } },
  { "image", { "CreateImage" }, { "DestroyImage" } },
  { "timer", { "CreateTimer" }, { "DestroyTimer" } },
  { "trigger", { "CreateTrigger" }, { "DestroyTrigger" } },
  { "condition", { "Condition", "Filter", "And", "Or", "Not" }, { "DestroyCondition", "DestroyFilter", "DestroyBoolExpr" } },
}
local handlesMade, handlesDestroyed = {}, {}
local handlesInstalled = false

function MoonwellHandlesInstall()
  if handlesInstalled then return end
  handlesInstalled = true
  for _, kind in ipairs(handleKinds) do
    local name = kind[1]
    handlesMade[name], handlesDestroyed[name] = 0, 0
    for _, native in ipairs(kind[2]) do
      local original = _G[native]
      if type(original) == "function" then
        _G[native] = function (...)
          local handle = original(...)
          if handle ~= nil then handlesMade[name] = handlesMade[name] + 1 end
          return handle
        end
      end
    end
    for _, native in ipairs(kind[3]) do
      local original = _G[native]
      if type(original) == "function" then
        _G[native] = function (handle, ...)
          if handle ~= nil then handlesDestroyed[name] = handlesDestroyed[name] + 1 end
          return original(handle, ...)
        end
      end
    end
  end
end

-- "trigger 120, effect 40, location 12": the kinds with handles alive (made less destroyed), the most first.
function MoonwellHandlesLine()
  local counts = {}
  for name, made in pairs(handlesMade) do
    local alive = made - handlesDestroyed[name]
    if alive ~= 0 then counts[name] = alive end
  end
  return kinds(counts)
end

-- The script line of the code that called the method this is written in (a timer's After or Every), read from the stack
-- with error's position: 2.4 us (game facts, 11-10). 0 when none is found.
function MoonwellMadeLine()
  local _, position = pcall(error, "", 4)
  local digits = type(position) == "string" and string.match(position, ":(%d+): $")
  return digits and tonumber(digits) or 0
end

-- "Spells/Rage.cs:12" for a script line, or "" for none (Moonwell's own code is not marked).
function MoonwellLineName(line)
  return MoonwellLocationOf(line) or ""
end

-- The profiler's timing of the map's reactions: while it runs, every method of a map's class whose name starts with On
-- is wrapped where the class defines it, so a call through any object of the class or its subclasses is timed; stopping
-- puts every one back. A class seen while it runs is wrapped then. The walk up a class's bases stops at Moonwell's own
-- (namespace Moonwell, and the generated Moonwell.Objects) and System's; the spell templates of Moonwell.Spells are timed.
local profileClasses = {}
local profileWrapped = nil

-- The debug world's clock, os.clock in the game, as the profiler's C# reads it.
local function clock()
  return Moonwell.DebugSession.Clock()
end

local function isMapClass(class)
  local name = rawget(class, "__name__")
  if type(name) ~= "string" then return false end
  return string.match(name, "^Moonwell%.[^%.]+$") == nil
    and string.sub(name, 1, 17) ~= "Moonwell.Objects."
    and string.sub(name, 1, 7) ~= "System."
end

local function shortName(class)
  local name = rawget(class, "__name__")
  return string.match(name, "([^%.%+]+)$") or name
end

local function finish(entry, started, ...)
  Moonwell.Profiler.Record(entry, clock() - started)
  return ...
end

local function wrapClass(class)
  local c = class
  while c ~= nil and isMapClass(c) do
    if profileWrapped[c] == nil then
      local originals = {}
      profileWrapped[c] = originals
      for key, value in pairs(c) do
        if type(key) == "string" and type(value) == "function" and string.sub(key, 1, 2) == "On" then
          originals[key] = value
        end
      end
      for key, original in pairs(originals) do
        local entry = Moonwell.Profiler.EntryOf(shortName(c) .. "." .. key)
        rawset(c, key, function (...)
          return finish(entry, clock(), original(...))
        end)
      end
    end
    c = getmetatable(c)
  end
end

function MoonwellProfileSeen(obj)
  local class = getmetatable(obj)
  if class == nil or profileClasses[class] then return end
  profileClasses[class] = true
  if profileWrapped ~= nil then wrapClass(class) end
end

function MoonwellProfileStart()
  if profileWrapped ~= nil then return end
  profileWrapped = {}
  for class in pairs(profileClasses) do wrapClass(class) end
end

function MoonwellProfileStop()
  if profileWrapped == nil then return end
  for class, originals in pairs(profileWrapped) do
    for key, original in pairs(originals) do rawset(class, key, original) end
  end
  profileWrapped = nil
end

-- The loop guard. Nothing ends a Lua call that runs on (game facts, 10-25), so a development build writes MoonwellLoop()
-- at the start of every loop body of the map's own code (LineMarks in Moonwell.Build): a call a turn, 29 ns (game facts,
-- 10-12). It counts the turns run while the game's time stands still, reading the time every 100,000 turns; at 100
-- such steps, 10,000,000 turns in one instant, Moonwell.LoopGuard.Stop raises an error at the loop's line. An empty loop
-- of that many turns has frozen the game for a second, so no honest loop gets there. The time is the game's own, the same
-- on every machine, and Local.For holds the count while its action runs, so every machine stops a loop at the same turn.
local loopStep, loopSteps = 100000, 100
local loopTurns, loopStepsRun, loopInstant
local heldTurns, heldStepsRun, heldInstant

function MoonwellLoopReset()
  loopTurns, loopStepsRun, loopInstant = 0, 0, nil
end
MoonwellLoopReset()

function MoonwellLoop()
  loopTurns = loopTurns + 1
  if loopTurns < loopStep then return end
  loopTurns = 0
  -- Static setup that runs while the script loads can loop before Moonwell's classes are there: one instant, then.
  local session = Moonwell and Moonwell.DebugSession
  local now = session and session.GameNow() or 0
  if now ~= loopInstant then
    loopInstant, loopStepsRun = now, 0
    return
  end
  loopStepsRun = loopStepsRun + 1
  if loopStepsRun < loopSteps then return end
  loopStepsRun = 0
  Moonwell.LoopGuard.Stop()
end

function MoonwellLoopHold()
  heldTurns, heldStepsRun, heldInstant = loopTurns, loopStepsRun, loopInstant
end

function MoonwellLoopRelease()
  loopTurns, loopStepsRun, loopInstant = heldTurns, heldStepsRun, heldInstant
end
