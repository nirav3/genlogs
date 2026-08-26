**Technical Exercise**

The Genlogs platform collects high-definition images from cameras positioned along major highways across the United States and processes them to track commercial trucks nationwide.

When an image is captured, the system first analyzes it to detect potential license plate characters, truck identification numbers, and company logos. Once a USDOT number is identified, the platform integrates with the [SAFER FMCSA API](https://safer.fmcsa.dot.gov) to associate the detected information with the corresponding carrier and vehicle records.

Additionally, the platform provides a web portal where users can specify an origin and destination city to identify which carriers are moving the highest volume of trucks between those two locations.

Your assignment is:

1. Read on detail all 5 points of this document, and before starting solving it, send to the hiring manager via email:  
   1. Step-by-step plan to complete each point of this document.  
   2. Time estimate to implement the plan, broken down by item in hours.  
   3. Delivery date and time.  
   4. Wait for the hiring manager's review and approval before proceeding with the rest of the test.  
2. Describe how you would architect/design the Genlogs platform. What modules/components would you create? How would the information flow between components? (diagram expected)  
3. How would you design the database and its tables for the Genlogs platform?  (diagram expected)  
4. Using **Open spec** framework for Spec-Driven Development (SDD), write a small application that simulates the portal. The information does not need to be stored in a database. . The following are the application specifications:  
   1. Front end Javascript client that catch the fields info and send it to back end server:  
      1. The application should capture the following fields on a **single** page:  
         1. From (city) \<- look match with google maps  
         2. To (city)  \<- look match with google maps  
         3. Button “Search”  
         4. Once the user clicks the search button, search a map that shows the fastest 3 routes between the 2 cities provider (embed Google maps)  
         5. Render a list of carriers that are returned from the back end.  
   2. Back end API :  
      1. Enable the endpoints that receive the data that comes from the front end (from city, to city):  
         1. From New York City to Washington DC:  
            1. Knight-Swift Transport Services (10 Trucks/Day)  
            2. J.B. Hunt Transport Services Inc (7 Trucks/Day)  
            3. YRC Worldwide (5 Trucks A day)  
         2. From San Francisco to Los Angeles:  
            1. XPO Logistics (9 Trucks/Day)  
            2. Schneider (6 Trucks/Day)  
            3. Landstar Systems (2 Trucks A day)  
         3. From a city different to NYC/SF or to a city different from Washington DC / Los Angeles  
            1. UPS Inc. (11 trucks Day)  
            2. FedEx Corp (9 trucks a day)  
   3. Share the url of the resulting code in a versioning server: Ie, Github, Gitlab, Bitbucket  
   4. Push on the repository the prompts and the rules used  
   5. Deploy project to a cloud provider (AWS, GCP, other), share the URL  
5. Send to the interviewer the amount of time you spent doing the test, if different with the estimated time of point 1 explain the reason.

If you have any questions in regards to the test don’t hesitate in sending an email to the interviewer.

Happy coding\!\!  
