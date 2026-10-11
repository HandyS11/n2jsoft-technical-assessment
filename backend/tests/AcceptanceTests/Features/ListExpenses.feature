Feature: List the expenses of a report

  Background:
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |

  Scenario: Listing returns the expenses by date, deleted ones left out
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description | Amount | Status  |
      | 2025-10-15 | Taxi        | 23.50  |         |
      | 2025-10-02 | Hôtel       | 120.00 |         |
      | 2025-10-09 | Train       | 45.00  | Deleted |
    When I list the expenses of "Juste Leblanc - Octobre 2025"
    Then the response status is 200
    And the expenses listed are
      | Date       | Description | Amount |
      | 2025-10-02 | Hôtel       | 120.00 |
      | 2025-10-15 | Taxi        | 23.50  |
    And the listing reports 2 expenses in total

  Scenario: Listing returns the requested page of 5 and the total number of expenses
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description |
      | 2025-10-01 | One         |
      | 2025-10-02 | Two         |
      | 2025-10-03 | Three       |
      | 2025-10-04 | Four        |
      | 2025-10-05 | Five        |
      | 2025-10-06 | Six         |
    When I list page 2 of the expenses of "Juste Leblanc - Octobre 2025" with 5 expenses per page
    Then the response status is 200
    And the expenses listed are
      | Date       | Description | Amount |
      | 2025-10-06 | Six         | 10.00  |
    And the listing reports 6 expenses in total

  Scenario: Listing returns no expense when the report has none
    When I list the expenses of "Juste Leblanc - Octobre 2025"
    Then the response status is 200
    And no expense is listed

  Scenario: Listing the expenses of an unknown report answers not found
    When I list the expenses of an unknown expense report
    Then the response is a problem with status 404

  Scenario: The expenses of a deleted user's report can still be listed
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description |
      | 2025-10-15 | Taxi        |
    When I delete the user "Juste Leblanc"
    And I list the expenses of "Juste Leblanc - Octobre 2025"
    Then the listing reports 1 expenses in total
