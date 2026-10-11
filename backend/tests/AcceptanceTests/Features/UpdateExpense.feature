Feature: Update an expense

  Background:
    Given the following users exist
      | First name | Last name | Monthly quota |
      | Juste      | Leblanc   | 1             |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    And the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description |
      | 2025-10-15 | Taxi        |

  Scenario: Updating an expense replaces its details, even at the quota
    When I update the expense "Taxi" with
      | Date       | Description | Amount | Store | Street         | Postal code | City      |
      | 2025-10-31 | Taxi        | 42.00  | G7    | 8 quai du Port | 13002       | Marseille |
    Then the response status is 204
    When I get the expense "Taxi"
    Then the expense returned is
      | Date       | Description | Amount | Store | Street         | Postal code | City      |
      | 2025-10-31 | Taxi        | 42.00  | G7    | 8 quai du Port | 13002       | Marseille |

  Scenario: An expense cannot move out of its report's month
    When I update the expense "Taxi" to the date "2025-11-01"
    Then the response is a business rule violation with the code "expense.date_outside_report_month"

  Scenario: Updating rejects invalid details
    When I update the expense "Taxi" whose amount is 0
    Then the response is a problem with status 400

  Scenario: Updating with the ETag read replaces the details
    When I read the expense "Taxi"
    And I update the expense "Taxi" with the ETag read
    Then the response status is 204

  Scenario: Updating with an ETag read before another change is refused
    When I read the expense "Taxi"
    And I update the expense "Taxi" to the date "2025-10-20"
    And I update the expense "Taxi" with the ETag read
    Then the response is a problem with status 412

  Scenario: A deleted user's report keeps its expenses as they are
    When I delete the user "Juste Leblanc"
    And I update the expense "Taxi" to the date "2025-10-20"
    Then the response is a business rule violation with the code "expense_report.deleted_user"

  Scenario: Updating a deleted expense answers not found
    When I delete the expense "Taxi"
    And I update the expense "Taxi" to the date "2025-10-20"
    Then the response is a problem with status 404

  Scenario: Updating an unknown expense answers not found
    When I update an unknown expense of "Juste Leblanc - Octobre 2025"
    Then the response is a problem with status 404
