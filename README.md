This solution is an implementation of the requirements written in the Instructions.md

To run this solution deploy.sh can be used.
Then a request to /api/readings should insert a new record to the database

Known issues:
- I wasn't able to run deploy.sh on windows but I converted it to PowerShell
Part of it worked ok but it stuck on creating the tables so I created them manually

- I'm using binary messages straight in the `/api/readings`
