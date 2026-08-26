# Yodaphone AI-powered Customer Service Chatbot

## Project Overview

This project is part of an assignment for Cranfield University Digital and Technology Solutions apprentices. A detailed description of the task, given in the assignment brief, can be found under the heading `The Assignment Task`.

## Developer Setup

For ease of setup and consistency we are using a Docker development container. 

New to C#, .NET, or Blazor? Use the [Yodaphone developer cheatsheet](DEVELOPER-CHEATSHEET.md) for the daily workflow, code examples, debugging, testing, and common fixes.

### Required Software

- Git
- VS Code
  - VS Code `Dev Containers` extension

#### On Windows

- Windows Subsystem for Linux 2
  - Open powershell and run `wsl --install` then restart your computer
- Docker Desktop [Windows](https://www.docker.com/products/docker-desktop/)
  - Choose the WSL 2 instalation

#### On MacOS
- Docker Desktop for Mac

### Setting up the dev container

After installing the required software, make sure Docker is running, then reopen VS Code. You will be notified that you can open the project in a container; accept and the VS Code window will reload, download the container, and run it.
Alternatively, you can manually reopen in container by pressing `Ctrl+Shift+P` and selecting `Dev Containers: Reopen in Container`.

You will then need to set your Git global config variables by running:
`git config --global user.name ["Your name"]` and `git config --global user.email [Your email address]`

## The Assignment Task

### Task

- As part of a UK-based telecommunications company (your group must create a realistic company name and organisational profile), you have been appointed to the Software Development Team to develop an AI-enabled Customer Service Chatbot.
- The organisation is experiencing increasing volumes of customer enquiries, technical support requests, service-related issues, and customer complaints. Existing support processes are becoming inefficient, resulting in longer response times, reduced customer satisfaction, and increased operational costs. Senior management has commissioned your team to design and develop an intelligent software solution that can automate routine customer interactions, improve service delivery, and enhance organisational productivity.
- Your task is to design, develop, test, and evaluate a functional AI-based customer service chatbot that addresses identified business requirements and demonstrates the application of advanced programming concepts and professional software engineering practices.

**You are required to:**

- Define and justify the business problem clearly, including its relevance to organisational objectives and your role as a software developer.
- Conduct a requirements analysis to identify both functional and non-functional requirements of the proposed system.
- Design an appropriate software solution and produce suitable design artefacts, such as:
  - Use Case Diagrams
  - Class Diagrams
  - Sequence Diagrams
  - Activity Diagrams
  - System Architecture Diagrams
  - User Interface Wireframes
- Justify all design decisions by demonstrating how they contribute to system usability, maintainability, scalability, security and performance.
- Implement the solution using an appropriate programming language and development framework, adhering to recognised coding standards, software engineering principles and industry best practices.
- Demonstrate the application of advanced programming techniques, including:
  - GUI development
  - Object-Oriented Programming principles
  - Multithreading, concurrency control, or asynchronous programming
  - Networking and data communication
  - Data storage, retrieval, and management
  - Error and exception handling
  - Integration with AI services, APIs, or machine learning components (where appropriate)
- Demonstrate the effective use of professional advanced programming tools and practices, such as:
  - Version control systems (e.g., Git)
  - Build and deployment processes
  - Automated testing
  - Debugging and logging tools
  - Code reviews and documentation
- Develop and execute an appropriate testing strategy, providing evidence of:
  - Unit testing
  - Integration testing
  - User acceptance testing
  - Test results and evaluation
- Evaluate and test your solution, ensuring both functional and non-functional requirements are met.
- Deliver a 15-minute presentation showcasing your software solution, supported by a poster and a live demonstration. This should be aimed at both technical and non-technical audiences, highlighting the problem addressed, your design and implementation approach, and the impact of your solution.

## Contributors
- S44635

## AI Use Acknowledgement

ChatGPT 5.6 Sol (OpenAI, https://chatgpt.com/) 


ChatGPT 5.6 Sol has been used in the initial code creation of this project. That includes:
- Setting up the default .NET dev container
- Establishing the default ASP.NET / Blazor project

All AI generated content has been subject to human review.