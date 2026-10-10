Feature: List users

  Scenario: Listing returns every user sorted by last name then first name
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
      | Marc       | Assin     |
      | Albert     | Leblanc   |
    When I list the users
    Then the response status is 200
    And the users listed are
      | Name           |
      | Marc Assin     |
      | Albert Leblanc |
      | Juste Leblanc  |

  Scenario: Listing returns no user when none exists
    When I list the users
    Then the response status is 200
    And no user is listed

  Scenario: Listing returns the requested page and the total number of users
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
      | Marc       | Assin     |
      | Albert     | Leblanc   |
    When I list page 2 of the users with 2 users per page
    Then the response status is 200
    And the users listed are
      | Name          |
      | Juste Leblanc |
    And the listing reports 3 users in total

  Scenario Outline: Listing rejects an invalid page
    When I request "/v1/users?<query>"
    Then the response is a problem with status 400

    Examples:
      | query         |
      | page=0        |
      | pageSize=0    |
      | pageSize=101  |
