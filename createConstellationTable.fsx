#r "nuget: System.Data.SQLite"

open System.IO
open System.Data.SQLite

type ConstellationData = {
    name : string
    stars : int list
}

type options = System.StringSplitOptions

let queryCreateConstellationTable = """ 
    CREATE TABLE IF NOT EXISTS constellations (
        id INTEGER NOT NULL,
        fullname TEXT NOT NULL
    );
"""

let queryCreateStarsConstellationTable = """
    CREATE TABLE IF NOT EXISTS stars_constellation (
        hip INTEGER NOT NULL,
        conId INTEGER NOT NULL
    )
"""

let constellationLinesFile = "./constellation_lines.dat"

let flatten (source : 'T seq seq) :'T seq =
    System.Linq.Enumerable.SelectMany(source, id)

let getConstellationInsertQueries (dataList: ConstellationData list) =
    dataList
    |> List.map (fun data -> data.name)
    |> List.mapi (fun i name -> sprintf """INSERT INTO constellations (id, fullname) VALUES(%d, "%s");""" (i + 1) name)

let getStarsConstellationInsertQueries (dataList: ConstellationData list) =
    dataList
    |> List.mapi (fun i data -> 
        data.stars
        |> List.map (fun hip -> sprintf """INSERT INTO stars_constellation (hip, conId) VALUES(%d, %d);""" hip (i + 1))
        |> List.toSeq)
    |> List.toSeq
    |> flatten
    |> Seq.toList

let setHeader (data: ConstellationData) (line: string) =
    let header = line.Substring(2)
    { data with name = header }

let setStarList (data: ConstellationData) (line: string) =
    let ids = Array.map (fun str -> int str) (line.Substring(1, line.Length - 2).Split(',', options.TrimEntries))
    { data with stars = List.concat [data.stars; Array.toList ids] }

let buildConstellationData (lines: string list) =

    let rec processLine (data: ConstellationData) (lineIndex: int) (headerAgain: bool) (dataList: ConstellationData list) =
        match lineIndex with
        | ind when ind >= lines.Length -> (data :: dataList)
        | _ ->
            let line = lines.[lineIndex]
            match line with
            | _ when lineIndex >= lines.Length -> dataList
            | x when not headerAgain && x.StartsWith('*') -> 
                processLine (setHeader data line) (lineIndex + 1) true dataList
            | x when headerAgain && x.StartsWith('*') ->            
                let newData = { name = ""; stars = List.Empty }
                processLine newData lineIndex false (data :: dataList)
            | x when x.StartsWith('[') ->
                processLine (setStarList data line) (lineIndex + 1) headerAgain dataList

    let data = { name = ""; stars = List.Empty }
    processLine data 0 false List.Empty

let lines = Array.toList (Array.where (fun line -> line <> "") (File.ReadAllLines constellationLinesFile))

let result = buildConstellationData lines

// printfn "%A" result

// ===================== SQLite =====================

let connectionString = sprintf "Data Source=%s;Version=3" "./swoa.db"
let connection = new SQLiteConnection(connectionString)
connection.Open()

let constellationTableCommand = new SQLiteCommand(queryCreateConstellationTable, connection)
constellationTableCommand.ExecuteNonQuery()

let starsConsTableCommand = new SQLiteCommand(queryCreateStarsConstellationTable, connection)
starsConsTableCommand.ExecuteNonQuery()

// printfn "%A" (getConstellationInsertQueries result)

//printfn "%A" (getStarsConstellationInsertQueries result)

// let constellationInsertQueries = getConstellationInsertQueries result

// for i in [0..(constellationInsertQueries.Length)] do
//     let insertConstellationsCommand = new SQLiteCommand(constellationInsertQueries.[i], connection)
//     insertConstellationsCommand.ExecuteNonQuery()

let starsConstellationInsertQueries = getStarsConstellationInsertQueries result
for i in [0..(starsConstellationInsertQueries.Length)] do
    let insertStarsConstellationCommand = new SQLiteCommand(starsConstellationInsertQueries.[i], connection)
    insertStarsConstellationCommand.ExecuteNonQuery()

connection.Close()