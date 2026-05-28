# README to the solution
This solution is an implementation of the requirements written in the Instructions.md

To run this solution deploy.sh can be used.
Then a request to `/api/readings` should insert a new record to the database
A request to `/api/readings/raw` should insert a new record to the database and deserialize data from base64

Known issues:
- I wasn't able to run deploy.sh on windows but I converted it to PowerShell
Part of it worked ok but it stuck on creating the tables so I created them manually

- For some reason `/api/readings/raw` endpoint returns 404 in kubernetes but works ok when running locally

I use port forwarding or
``` sh
minikube service metersystem-api
```
to open access to the API service. The API runs on port 8080 internally.

## Future improvements
- To make the solution runnable on windows.
- To investigate the issue with `/api/readings/raw` endpoint in kubernetes.
  I think the issue is related to the fact that the solution was not buit correctly or the container is replaced incorrectly.
  It might use a container with an older build.
- To use EasyNetQ instead of RabbitMQ.Client to simplify the code and make it more robust.
  Another option would be NServiceBus (debateble because it is not free is some cases but adds message retries and advanced monitoring).
- To move out the settings from `appsettings.json` to Kubernetes configuration.
- To remove code duplication if possible in Worker when adding a new reading to the database
