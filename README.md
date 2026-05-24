# README to the solution
This solution is an implementation of the requirements written in the Instructions.md

To run this solution deploy.sh can be used.
Then a request to `/api/readings` should insert a new record to the database
A request to `/api/readings/raw` should insert a new record to the database and deserialize data from base64

Known issues:
- I wasn't able to run deploy.sh on windows but I converted it to PowerShell
Part of it worked ok but it stuck on creating the tables so I created them manually

- For some reason `/api/readings/raw` endpoint returns 404 in kubernetes but works ok when running locally
